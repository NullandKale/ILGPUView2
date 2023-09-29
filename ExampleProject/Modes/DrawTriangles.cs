using GPU;
using ILGPU.Runtime;
using ILGPU;

namespace ExampleProject.Modes
{
    public unsafe struct DrawTriangles : ITriangleImageFilter
    {
        public Mat4x4 rotationMatrix;

        public DrawTriangles(int tick)
        {
            rotationMatrix = Mat4x4.CreateMVPMatrix(tick, 1.77f, 0, float.MaxValue);
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

                if (isInTriangle && depth <= currentDistance)
                {
                    currentDistance = depth;
                    currentColor = ComputeColorFromTriangle(x, y, triangle, (float)i / (float)triangles.Length);
                }
            }

            return currentColor;
        }
        private (bool, float) IsPointInTriangle(float x, float y, Triangle triangle)
        {
            // Rotate triangle vertices
            Vec3 v0 = rotationMatrix.MultiplyVector(triangle.v0);
            Vec3 v1 = rotationMatrix.MultiplyVector(triangle.v1);
            Vec3 v2 = rotationMatrix.MultiplyVector(triangle.v2);

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

        private RGBA32 ComputeColorFromTriangle(float x, float y, Triangle triangle, float i)
        {
            //return new RGBA32((byte)(x * 255), (byte)(y * 255), 255, 255);
            //return new RGBA32((byte)(x * 255), (byte)(y * 255), 255, 255);
            return new RGBA32((byte)(i * 255), (byte)(i * 255), (byte)(i * 255), 255);
        }
    }

}
