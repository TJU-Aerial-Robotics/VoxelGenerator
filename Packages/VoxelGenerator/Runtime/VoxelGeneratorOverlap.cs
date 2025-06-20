/* 
 *  Copyright 2025 Hongyu Cao
 *  
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *  
 *      http://www.apache.org/licenses/LICENSE-2.0
 *  
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelGenerator.Jobs;

namespace VoxelGenerator {
    public class VoxelGeneratorOverlap : VoxelGeneratorBase {
        // NOTICE: use this to generate voxel point cloud data of the static obstacles
        [SerializeField, Range(1, MAX_DEPTH)] private int _objectDensity = 8; // 1 means no density, 16 means full density
        [SerializeField, ReadOnly] private int _depth = 8;
        [SerializeField, ReadOnly] private float _voxelCoarsestSize = 64;
        private NativeList<OverlapSphereCommand> _currentCommands;
        private NativeList<OverlapSphereCommand> _childCommands;
        private NativeArray<float3> _octantOffsets;
        private const int MAX_DEPTH = 16; // Maximum depth to prevent overflow
        private const int OVERLAP_SPHERE_COMMAND_SIZE = 36;

        protected override float CalculateFinestVoxelCount() {
            var finestVoxelSize = _bounds.size / _voxelSize;
            var finestVoxelCount = finestVoxelSize.x * finestVoxelSize.y * finestVoxelSize.z;
            Debug.Log($"Total finest voxel numbers: {finestVoxelCount}");
            return finestVoxelCount;
        }

        protected override IEnumerator ProcessVoxel() {
            for (int currentDepth = 0; currentDepth < _depth; currentDepth++) {
                yield return ProcessVoxelDepth(currentDepth);
            }
        }

        protected override void DisposeDerivedNativeCollections() {
            DisposeList(ref _currentCommands);
            DisposeList(ref _childCommands);
            DisposeList(ref _octantOffsets);
        }

        protected override void DisposeAfterCalculate() {
            DisposeList(ref _currentCommands);
            DisposeList(ref _childCommands);
        }

        protected override void UpdateInfo() {
            base.UpdateInfo();

            var minBoundSize = Mathf.Min(_bounds.size.x, _bounds.size.y, _bounds.size.z);
            if (minBoundSize <= 0)
                throw new ArgumentException("Bounds size must be greater than zero.");

            _voxelCoarsestSize = minBoundSize / 2;
            var depth = Mathf.CeilToInt(Mathf.Log(_voxelCoarsestSize / _voxelSize, 2));
            _depth = Mathf.Clamp(depth, 1, MAX_DEPTH + 1 - _objectDensity);
            _voxelCoarsestSize = Mathf.Pow(2, _depth - 1) * _voxelSize;

            if (_verbose)
                Debug.Log($"Depth: {_depth}, Voxel Size: {_voxelSize}, Coarsest Voxel Size: {_voxelCoarsestSize}");
        }

        protected override IEnumerator InitVoxelCmds() {
            int voxelsX, voxelsY, voxelsZ;
            voxelsX = Mathf.CeilToInt(_bounds.size.x / _voxelCoarsestSize);
            voxelsY = Mathf.CeilToInt(_bounds.size.y / _voxelCoarsestSize);
            voxelsZ = Mathf.CeilToInt(_bounds.size.z / _voxelCoarsestSize);
            int totalVoxels = voxelsX * voxelsY * voxelsZ;

            _currentCommands = new NativeList<OverlapSphereCommand>(totalVoxels, Allocator.Persistent);
            _currentCommands.ResizeUninitialized(totalVoxels);
            _childCommands = new NativeList<OverlapSphereCommand>(Allocator.Persistent);
            _voxelPointCloudData = new NativeList<float3>(Allocator.Persistent);

            _octantOffsets = new NativeArray<float3>(8, Allocator.Persistent);
            for (byte idx = 0; idx < 8; idx++) {
                _octantOffsets[idx] = new float3(
                    (idx & 1) == 0 ? -1 : 1,
                    (idx & 2) == 0 ? -1 : 1,
                    (idx & 4) == 0 ? -1 : 1
                );
            }

            float radius = (_depth == 1) ? _voxelCoarsestSize / 2f : _voxelCoarsestSize / 2f * Mathf.Sqrt(3);
            var queryParameters = new QueryParameters(_layerMask);
            var overlapCmdSetupJob = new OverlapCmdSetupJob {
                voxelsX = voxelsX,
                voxelsY = voxelsY,
                voxelsZ = voxelsZ,
                voxelSize = _voxelCoarsestSize,
                radius = radius,
                bounds = _bounds,
                queryParameters = queryParameters,
                currentCmds = _currentCommands.AsArray()
            };

            var initJobHandle = overlapCmdSetupJob.ScheduleParallelByRef(totalVoxels, INIT_JOB_BATCH_COUNT, default);
            yield return new WaitUntil(() => initJobHandle.IsCompleted);
            initJobHandle.Complete();
        }

        private IEnumerator ProcessVoxelDepth(int currentDepth) {
            float currentVoxelFull = Mathf.Pow(2, _depth - 1 - currentDepth) * _voxelSize;
            float childVoxelFull = currentVoxelFull / 2;
            float childVoxelHalf = currentVoxelFull / 4;

            bool childrenAreForLastDepth = (currentDepth >= _depth - 2);
            bool isProcessingLastDepth = (currentDepth == _depth - 1);

            float childRadius = childrenAreForLastDepth ? childVoxelHalf : childVoxelHalf * Mathf.Sqrt(3);
            int currentCmdCount = _currentCommands.Length;
            if (currentCmdCount <= 0) {
                Debug.LogWarning($"No commands to process at depth {currentDepth}. Skipping processing.");
                yield break;
            }

            int estimatedNextCapacity = currentCmdCount * (isProcessingLastDepth ? 1 : 8);
            if (_verbose) {
                Debug.Log($"-------- Processing depth: {currentDepth + 1}/{_depth} --------");
                float tryToResizeMB = estimatedNextCapacity * OVERLAP_SPHERE_COMMAND_SIZE / (1024 * 1024);
                float tryToAllocateMB = math.ceilpow2(estimatedNextCapacity) * OVERLAP_SPHERE_COMMAND_SIZE / (1024 * 1024);
                Debug.Log($"Trying to resize {tryToResizeMB:F3} MB for child commands. Allocated size will be {tryToAllocateMB:F3} MB.");
            }

            var hitResults = new NativeArray<ColliderHit>(currentCmdCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            Physics.SyncTransforms();
            var overlapJobHandle = OverlapSphereCommand.ScheduleBatch(_currentCommands.AsArray(), hitResults, PHYSICS_BATCH_COUNT, 1);

            if (estimatedNextCapacity > _childCommands.Capacity) {
                _childCommands.SetCapacity(estimatedNextCapacity);
                if (_verbose)
                    Debug.Log($"Estimated next capacity ({estimatedNextCapacity}) "
                    + $"exceeds current child commands capacity ({_childCommands.Capacity}). "
                    + "Resizing child commands.");
            }

            if (isProcessingLastDepth) {
                _voxelPointCloudData.SetCapacity(estimatedNextCapacity);
            }


            var handleColliderHitJob = new HandleColliderHitJob {
                isProcessingLastDepth = isProcessingLastDepth,
                bounds = _bounds,
                coordinateType = _coordinateType,
                hitResults = hitResults,
                currentCmds = _currentCommands.AsArray(),
                childCmds = _childCommands,
                childVoxelFull = childVoxelFull,
                childVoxelHalf = childVoxelHalf,
                offsets = _octantOffsets,
                childRadius = childRadius,
                voxelPointCloudData = _voxelPointCloudData
            };

            var handleColliderHitJobHandle = handleColliderHitJob.ScheduleByRef(_currentCommands.Length, overlapJobHandle);
            yield return new WaitUntil(() => handleColliderHitJobHandle.IsCompleted);
            handleColliderHitJobHandle.Complete();
            hitResults.Dispose();

            var temp = _currentCommands;
            _currentCommands = _childCommands;
            _childCommands = temp;
            _childCommands.Clear();
        }
    }
}
