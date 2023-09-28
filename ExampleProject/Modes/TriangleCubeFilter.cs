using GPU;
using System;
using ILGPU.Runtime;
using ILGPU;

namespace ExampleProject.Modes
{
    public unsafe struct DrawTriangles : ITriangleImageFilter
    {
        public fixed float rotationMatrix[9];

        public DrawTriangles(int tick)
        {
            // Calculate rotation angles based on tick
            float angleX = (tick % 360) * (MathF.PI / 180);
            float angleY = (tick % 360) * (MathF.PI / 180);

            // 3D Rotation matrix for X and Y (ignoring Z for now)
            rotationMatrix[0] = MathF.Cos(angleY);
            rotationMatrix[1] = MathF.Sin(angleX) * MathF.Sin(angleY);
            rotationMatrix[2] = -MathF.Cos(angleX) * MathF.Sin(angleY);
            rotationMatrix[3] = 0;
            rotationMatrix[4] = MathF.Cos(angleX);
            rotationMatrix[5] = MathF.Sin(angleX);
            rotationMatrix[6] = MathF.Sin(angleY);
            rotationMatrix[7] = -MathF.Sin(angleX) * MathF.Cos(angleY);
            rotationMatrix[8] = MathF.Cos(angleX) * MathF.Cos(angleY);
        }

        private Vec3 RotateVertex(Vec3 vertex)
        {
            float x = rotationMatrix[0] * vertex.x + rotationMatrix[1] * vertex.y + rotationMatrix[2] * vertex.z;
            float y = rotationMatrix[3] * vertex.x + rotationMatrix[4] * vertex.y + rotationMatrix[5] * vertex.z;
            float z = rotationMatrix[6] * vertex.x + rotationMatrix[7] * vertex.y + rotationMatrix[8] * vertex.z;
            return new Vec3(x, y, z);
        }


        public RGBA32 Draw(int tick, float x, float y, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles)
        {
            // Map x and y from [0, 1] to [-1, 1]
            float mappedX = x * 2.0f - 1.0f;
            float mappedY = y * 2.0f - 1.0f;

            // Loop through all triangles and determine the color for the pixel at (x, y)
            float currentDistance = float.MaxValue;
            RGBA32 currentColor = new RGBA32(0, 0, 0, 255); // Default to Black

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle triangle = triangles[i];
                var (isInTriangle, depth) = IsPointInTriangle(mappedX, mappedY, triangle);

                if (isInTriangle && currentDistance > depth)
                {
                    currentDistance = depth;
                    currentColor = ComputeColorFromTriangle(x, y, triangle, i);
                }
            }

            return currentColor;
        }
        private (bool, float) IsPointInTriangle(float x, float y, Triangle triangle)
        {
            // Rotate triangle vertices
            Vec3 v0 = RotateVertex(triangle.v0);
            Vec3 v1 = RotateVertex(triangle.v1);
            Vec3 v2 = RotateVertex(triangle.v2);

            // Compute vectors and areas in barycentric coordinates
            float vec_x1 = v1.x - v0.x;
            float vec_y1 = v1.y - v0.y;
            float vec_x2 = v2.x - v0.x;
            float vec_y2 = v2.y - v0.y;
            float vec_px = x - v0.x;
            float vec_py = y - v0.y;

            float det = vec_x1 * vec_y2 - vec_x2 * vec_y1;

            // Backface culling
            if (det > 0)
            {
                return (false, float.MaxValue);
            }

            float invDet = 1.0f / det;

            // Calculate barycentric coordinates
            float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
            float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
            float gamma = 1.0f - alpha - beta;

            // Check if the point is inside the triangle
            bool isInTriangle = (alpha >= 0 && alpha <= 1) &&
                                (beta >= 0 && beta <= 1) &&
                                (gamma >= 0 && gamma <= 1);

            if (isInTriangle)
            {
                // Compute the depth at this point using barycentric coordinates
                float depth = alpha * v0.z + beta * v1.z + gamma * v2.z;
                return (true, depth);
            }

            return (false, float.MaxValue);
        }


        private bool IsInRange(Vec3 vec)
        {
            return vec.x >= 0 && vec.x <= 1 && vec.y >= 0 && vec.y <= 1 && vec.z >= 0 && vec.z <= 1;
        }

        private RGBA32 ComputeColorFromTriangle(float x, float y, Triangle triangle, int index)
        {
            return new RGBA32((byte)(x * 255), (byte)(y * 255), 255, 255);
        }



    }

}
