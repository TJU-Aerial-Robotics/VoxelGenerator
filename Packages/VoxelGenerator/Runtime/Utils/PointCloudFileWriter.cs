using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelGenerator {
    class PointCloudFileWriter {
        public static string Write(string folderName, string fileName,
            PointCloudFileType pointCloudFileType, NativeArray<float3> voxelPointCloudData) {
            var basePath = Directory.GetParent(Application.dataPath).FullName;
            string fullFolderPath = Path.Combine(basePath, folderName);
            string ext = pointCloudFileType == PointCloudFileType.PCDBin ? "pcd" : "ply";
            string pointCloudFileWithExt = $"{fileName}.{ext}";
            string fullFilePath = Path.Combine(fullFolderPath, pointCloudFileWithExt);

            if (!Directory.Exists(fullFolderPath)) {
                Directory.CreateDirectory(fullFolderPath);
            }
            using var fileStream = new FileStream(fullFilePath, FileMode.Create);

            WriteFileHeader(fileStream, voxelPointCloudData.Length, pointCloudFileType);
            WriteFileContent(fileStream, voxelPointCloudData);

            return fullFilePath;
        }
        private static void WriteFileHeader(FileStream fileStream, int pointCount, PointCloudFileType pointCloudFileType) {
            using StreamWriter streamWriter = new(fileStream, System.Text.Encoding.ASCII, 1024, leaveOpen: true);
            switch (pointCloudFileType) {
                case PointCloudFileType.PCDBin:
                    // // Write PCD header
                    streamWriter.Write("# .PCD v0.7 - Point Cloud Data file format\n");
                    streamWriter.Write("VERSION 0.7\n");
                    streamWriter.Write("FIELDS x y z\n");
                    streamWriter.Write("SIZE 4 4 4\n");
                    streamWriter.Write("TYPE F F F\n");
                    streamWriter.Write("COUNT 1 1 1\n");
                    streamWriter.Write($"WIDTH {pointCount}\n");
                    streamWriter.Write("HEIGHT 1\n");
                    streamWriter.Write("VIEWPOINT 0 0 0 1 0 0 0\n");
                    streamWriter.Write($"POINTS {pointCount}\n");
                    streamWriter.Write("DATA binary\n");
                    break;

                case PointCloudFileType.PLYBin:
                    // Write PLY header
                    streamWriter.Write("ply\n");
                    streamWriter.Write("format binary_little_endian 1.0\n");
                    streamWriter.Write($"element vertex {pointCount}\n");
                    streamWriter.Write("property float x\n");
                    streamWriter.Write("property float y\n");
                    streamWriter.Write("property float z\n");
                    streamWriter.Write("end_header\n");
                    break;
            }
        }
        private static void WriteFileContent(FileStream fileStream, NativeArray<float3> voxelPointCloudData) {
            using BinaryWriter binaryWriter = new(fileStream);
            var dataBytes = voxelPointCloudData.Reinterpret<byte>(12);
            binaryWriter.Write(dataBytes);
        }
    }
    public enum CoordinateType {
        Unity,
        FLU
    }

    public enum PointCloudFileType {
        PCDBin,
        PLYBin
    }

}
