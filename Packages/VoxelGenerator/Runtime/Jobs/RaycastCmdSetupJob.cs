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
