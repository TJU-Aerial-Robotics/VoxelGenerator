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

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelGenerator.Jobs {
    [BurstCompile]
    struct HandleColliderHitJob : IJobFor {
        public bool isProcessingLastDepth;
        public float childRadius;
        public float childVoxelFull;
        public float childVoxelHalf;
        public Bounds bounds;
        public CoordinateType coordinateType;
        [ReadOnly] public NativeArray<float3> offsets;
        [ReadOnly] public NativeArray<ColliderHit> hitResults;
        [ReadOnly] public NativeArray<OverlapSphereCommand> currentCmds;
        [WriteOnly] public NativeList<OverlapSphereCommand> childCmds;
        [WriteOnly] public NativeList<float3> voxelPointCloudData;
        public void Execute(int i) {
            if (hitResults[i].instanceID == 0)
                return;

            var currentCmd = currentCmds[i];
            if (isProcessingLastDepth) {
                switch (coordinateType) {
                    case CoordinateType.Unity:
                        voxelPointCloudData.Add(currentCmd.point);
                        break;
                    case CoordinateType.FLU:
                        voxelPointCloudData.Add(new float3(currentCmd.point.z, -currentCmd.point.x, currentCmd.point.y));
                        break;
                }
                return;
            }

            for (byte idx = 0; idx < 8; idx++) {
                float3 childPos = (float3)currentCmd.point + offsets[idx] * childVoxelHalf;
                Bounds childBounds = new(childPos, (float3)childVoxelFull);
                if (!bounds.Intersects(childBounds)) continue;
                childCmds.Add(new OverlapSphereCommand(childPos, childRadius, currentCmd.queryParameters));
            }
        }
    }
}