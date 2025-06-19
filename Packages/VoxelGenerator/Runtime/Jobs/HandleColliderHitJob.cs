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