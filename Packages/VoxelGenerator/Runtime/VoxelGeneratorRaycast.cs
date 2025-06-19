using System.Collections;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelGenerator.Jobs;

namespace VoxelGenerator {
    public class VoxelGeneratorRaycast : VoxelGeneratorBase {
        // NOTICE: use this to generate voxel point cloud data of the terrain
        private NativeArray<RaycastCommand> _raycastCommands;

        protected override float CalculateFinestVoxelCount() {
            var finestVoxelSize = _bounds.size / _voxelSize;
            var finestVoxelCount = finestVoxelSize.x * finestVoxelSize.z;
            Debug.Log($"Total finest voxel numbers: {finestVoxelCount}");
            return finestVoxelCount;
        }

        protected override IEnumerator ProcessVoxel() {
            var hitResults = new NativeArray<RaycastHit>(_raycastCommands.Length, Allocator.Persistent);
            Physics.SyncTransforms();
            var overlapJobHandle = RaycastCommand.ScheduleBatch(_raycastCommands, hitResults, PHYSICS_BATCH_COUNT, 1);
            var handleRaycastHitJob = new HandleRaycastHitJob {
                hitResults = hitResults,
                coordinateType = _coordinateType,
                raycastCommands = _raycastCommands,
                voxelPointCloudData = _voxelPointCloudData,
            };

            var handleRaycastHitJobHandle = handleRaycastHitJob.ScheduleByRef(_raycastCommands.Length, overlapJobHandle);
            yield return new WaitUntil(() => handleRaycastHitJobHandle.IsCompleted);
            handleRaycastHitJobHandle.Complete();
            hitResults.Dispose();
        }

        protected override void DisposeDerivedNativeCollections() {
            DisposeList(ref _raycastCommands);
        }

        protected override void DisposeAfterCalculate() {
            DisposeList(ref _raycastCommands);
        }

        protected override void UpdateInfo() {
            base.UpdateInfo();
            var minPlaneSize = Mathf.Min(_bounds.size.x, _bounds.size.z);
            if (minPlaneSize <= 0)
                throw new System.ArgumentException("Bounds size must be greater than zero.");

            if (_verbose)
                Debug.Log($"Voxel Size: {_voxelSize}");
        }

        protected override IEnumerator InitVoxelCmds() {
            int voxelsX, voxelsZ;
            voxelsX = Mathf.CeilToInt(_bounds.size.x / _voxelSize);
            voxelsZ = Mathf.CeilToInt(_bounds.size.z / _voxelSize);
            int totalVoxels = voxelsX * voxelsZ;

            _raycastCommands = new NativeArray<RaycastCommand>(totalVoxels, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _voxelPointCloudData = new NativeList<float3>(Allocator.Persistent);

            var queryParameters = new QueryParameters(_layerMask);
            var raycastCmdSetupJob = new RaycastCmdSetupJob {
                voxelsX = voxelsX,
                voxelsZ = voxelsZ,
                voxelSize = _voxelSize,
                distance = _bounds.size.y,
                bounds = _bounds,
                queryParameters = queryParameters,
                raycastCommands = _raycastCommands,
            };

            var initJobHandle = raycastCmdSetupJob.ScheduleParallelByRef(totalVoxels, INIT_JOB_BATCH_COUNT, default);
            yield return new WaitUntil(() => initJobHandle.IsCompleted);
            initJobHandle.Complete();
        }
    }
}
