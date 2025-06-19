using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelGenerator.Jobs {
    [BurstCompile]
    struct OverlapCmdSetupJob : IJobFor {
        public int voxelsX;
        public int voxelsY;
        public int voxelsZ;
        public float voxelSize;
        public float radius;
        public Bounds bounds;
        public QueryParameters queryParameters;
        [WriteOnly] public NativeArray<OverlapSphereCommand> currentCmds;
        public void Execute(int idx) {
            int3 index3D;
            index3D.x = idx % voxelsX;
            index3D.y = idx / voxelsX % voxelsY;
            index3D.z = idx / (voxelsX * voxelsY);

            float3 voxelPos = (float3)bounds.min + (float3)index3D * voxelSize;

            currentCmds[idx] = new OverlapSphereCommand(voxelPos, radius, queryParameters);
        }
    }
}