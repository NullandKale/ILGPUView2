using GPU;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleProject.Modes.Rasterizer
{
    public static class MeshLoader
    {
        public static GPUMesh LoadObjFile(Device device, int mesh_id, Mat4x4 transform, string filepath)
        {
            List<Vec3> vert_positions = new List<Vec3>();
            List<int> triangle_indices = new List<int>();
            List<Vec2> vert_uvs = new List<Vec2>();

            string[] lines = File.ReadAllLines(filepath);

            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "v":
                        vert_positions.Add(new Vec3(
                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture),
                            float.Parse(parts[3], CultureInfo.InvariantCulture)));
                        break;

                    case "vt":
                        vert_uvs.Add(new Vec2(
                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                            1.0f - float.Parse(parts[2], CultureInfo.InvariantCulture))); // Invert the V coordinate since OBJ uses top-left origin for textures
                        break;

                    case "f":
                        for (int i = 1; i < parts.Length - 1; i++)
                        {
                            string[] faceIndices = parts[i].Split('/');
                            string[] faceIndicesNext = parts[i + 1].Split('/');

                            triangle_indices.Add(int.Parse(faceIndices[0]) - 1);
                            triangle_indices.Add(int.Parse(faceIndicesNext[0]) - 1);
                            triangle_indices.Add(int.Parse(parts[1].Split('/')[0]) - 1);
                        }
                        break;
                }
            }

            return new GPUMesh(device, mesh_id, 0, vert_positions, triangle_indices, vert_uvs, transform);
        }
    }

}
