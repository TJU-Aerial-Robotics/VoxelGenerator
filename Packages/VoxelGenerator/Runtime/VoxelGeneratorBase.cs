using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelGenerator {
    public abstract class VoxelGeneratorBase : MonoBehaviour {
        [SerializeField, Min(0.01f)] protected float _voxelSize = 0.1f;
        [SerializeField] protected LayerMask _layerMask = 1;
        [SerializeField] protected bool _drawDebug = false;
        [SerializeField] protected bool _verbose = false;
        [SerializeField] protected bool _saveFile = false;
        [SerializeField] protected PointCloudFileType _pointCloudFileType = PointCloudFileType.PLYBin;
        [SerializeField] protected CoordinateType _coordinateType = CoordinateType.Unity;
        [SerializeField] protected string _pointCloudFolderName = "PointCloud";
        [SerializeField] protected string _pointCloudFileName = "pointcloud";
        protected NativeList<float3> _voxelPointCloudData;
        protected float _voxelSizeLocked;
        protected Bounds _bounds;
        protected bool _isRunning = false;
        private const int MAX_DRAW_VOXELS = 100_000;
        protected const int MAX_VOXEL_COUNT = (int)1e9;
        protected const int INIT_JOB_BATCH_COUNT = 64;
        protected const int PHYSICS_BATCH_COUNT = 16;

        protected abstract float CalculateFinestVoxelCount();
        protected abstract IEnumerator ProcessVoxel();
        protected abstract void DisposeDerivedNativeCollections();
        protected abstract void DisposeAfterCalculate();
        protected abstract IEnumerator InitVoxelCmds();

        void OnValidate() {
            UpdateInfo();
        }

        virtual protected void UpdateInfo() {
            _bounds.center = transform.position;
            _bounds.size = transform.localScale;
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.P))
                StartGeneration();
            else if (Input.GetKeyDown(KeyCode.R))
                ClearPointCloud();
        }
        public void StartGeneration() {
            if (!_isRunning) {
                StartCoroutine(GeneratePointCloud());
            }
            else {
                Debug.LogWarning("Point cloud generation is already running.");
            }
        }

        public IEnumerator GeneratePointCloud() {
            _isRunning = true;

            DisposeAllLists();

            if (!CheckVoxelCountLimit())
                yield break;

            var startTime = Time.realtimeSinceStartup;

            yield return CalculatePointCloud();

            DisposeAfterCalculate();

            var endTime = Time.realtimeSinceStartup;
            var generationTime = endTime - startTime;

            startTime = Time.realtimeSinceStartup;
            if (_saveFile)
                SavePointCloud();
            endTime = Time.realtimeSinceStartup;
            var fileWriteTime = endTime - startTime;

            var totalTime = generationTime + fileWriteTime;
            Debug.Log($"Finished in {totalTime:F3} seconds. "
            + $"Point cloud generation completed in {generationTime:F3} seconds. "
            + $"File write completed in {fileWriteTime:F3} seconds.");

            Debug.Log($"Total point cloud data size: {_voxelPointCloudData.Length} points.");
            if (_verbose) {
                // Calculate voxelPointCloudData size in MB
                float dataSizeMB = _voxelPointCloudData.Length * sizeof(float) * 3 / (1024f * 1024f);
                float allocatedDataSizeMB = _voxelPointCloudData.Capacity * sizeof(float) * 3 / (1024f * 1024f);
                Debug.Log($"Total point cloud data size: {dataSizeMB:F3} MB. Allocated data size {allocatedDataSizeMB:F3} MB.");
            }
            _voxelSizeLocked = _voxelSize; // Lock the voxel size after generation
            _isRunning = false;
        }

        private bool CheckVoxelCountLimit() {
            float finestVoxelCount = CalculateFinestVoxelCount();
            if (finestVoxelCount > MAX_VOXEL_COUNT) {
                Debug.LogError($"Total finest voxel numbers ({finestVoxelCount}) exceeds 1 billion, "
                + "which may cause performance issues or crashes. Consider increasing the voxel size or reducing the bounds size.");
                return false;
            }
            return true;
        }

        private IEnumerator CalculatePointCloud() {
            UpdateInfo();

            yield return InitVoxelCmds();
            yield return ProcessVoxel();
        }

        private void SavePointCloud() {
            if (!_voxelPointCloudData.IsCreated || _voxelPointCloudData.Length == 0) {
                Debug.LogWarning("No voxel point cloud data to save.");
                return;
            }
            string fullFilePath = PointCloudFileWriter.Write(
                _pointCloudFolderName,
                _pointCloudFileName,
                _pointCloudFileType,
                _voxelPointCloudData.AsArray());

            Debug.Log($"Point cloud data saved to {fullFilePath}");
        }

        private void OnDestroy() {
            DisposeAllLists();
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.localScale);

            if (_drawDebug && !_isRunning && _voxelPointCloudData.IsCreated) {
                Gizmos.color = Color.red;
                if (_voxelPointCloudData.Length > MAX_DRAW_VOXELS) {
                    Debug.LogWarning($"Too many voxels to draw ({_voxelPointCloudData.Length}). "
                    + "Only drawing the first {MAX_DRAW_VOXELS} voxels.");
                }
                for (int i = 0; i < math.min(_voxelPointCloudData.Length, MAX_DRAW_VOXELS); i++) {
                    var point = _voxelPointCloudData[i];
                    Gizmos.DrawWireCube(point, (float3)_voxelSizeLocked);
                }
            }
        }

        public void ClearPointCloud() {
            if (_voxelPointCloudData.IsCreated)
                _voxelPointCloudData.Dispose();
        }

        private void DisposeAllLists() {
            DisposeList(ref _voxelPointCloudData);
            DisposeDerivedNativeCollections();
        }

        protected void DisposeList<T>(ref NativeList<T> list) where T : unmanaged {
            if (list.IsCreated) {
                list.Dispose();
                list = default;
            }
        }

        protected void DisposeList<T>(ref NativeArray<T> list) where T : unmanaged {
            if (list.IsCreated) {
                list.Dispose();
                list = default;
            }
        }
    }
}