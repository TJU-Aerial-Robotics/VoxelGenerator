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
    struct HandleRaycastHitJob : IJobFor {
        public CoordinateType coordinateType;
        [ReadOnly] public NativeArray<RaycastHit> hitResults;
        [ReadOnly] public NativeArray<RaycastCommand> raycastCommands;
        [WriteOnly] public NativeList<float3> voxelPointCloudData;
        public void Execute(int i) {
            if (hitResults[i].distance == 0)
                return;

            var currentCmd = raycastCommands[i];
            float3 voxelPos = currentCmd.from + currentCmd.direction * hitResults[i].distance;

            switch (coordinateType) {
                case CoordinateType.Unity:
                    voxelPointCloudData.Add(voxelPos);
                    break;
                case CoordinateType.FLU:
                    voxelPointCloudData.Add(new float3(voxelPos.z, -voxelPos.x, voxelPos.y));
                    break;
            }
        }
    }
}
