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
