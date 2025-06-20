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
    struct RaycastCmdSetupJob : IJobFor {
        public int voxelsX;
        public int voxelsZ;
        public float voxelSize;
        public float distance;
        public Bounds bounds;
        public QueryParameters queryParameters;
        [WriteOnly] public NativeArray<RaycastCommand> raycastCommands;
        public void Execute(int i) {
            int3 index3D;
            index3D.x = i % voxelsX;
            index3D.z = i / voxelsX % voxelsZ;
            index3D.y = 0; // For raycasting, we only need the x and z indices, y is always 0

            float3 voxelPos = (float3)bounds.min + (float3)index3D * voxelSize;
            // Adjust the y position to be at the top of the voxel
            voxelPos.y = bounds.max.y;

            raycastCommands[i] = new RaycastCommand(voxelPos, Vector3.down, queryParameters, distance);
        }
    }
}
