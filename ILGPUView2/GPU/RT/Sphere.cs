using ILGPU;
using ILGPU.Runtime;
using System;

namespace GPU.RT
{
    public class RTTriangle
    {
        public static bool Intersect(ArrayView1D<Vec3, Stride1D.Dense> verts, int offset, Ray ray, out float t)
        {
            Vec3 vert0 = verts[(offset * 3) + 0];
            Vec3 vert1 = verts[(offset * 3) + 1];
            Vec3 vert2 = verts[(offset * 3) + 2];

            // Compute edge vectors
            Vec3 edge1 = vert1 - vert0;
            Vec3 edge2 = vert2 - vert0;

            // Compute determinant
            Vec3 pvec = Vec3.cross(ray.b, edge2);
            float det = Vec3.dot(edge1, pvec);

            // Check if the ray is parallel to the triangle
            if (Math.Abs(det) < 1e-6f)
            {
                t = float.MaxValue;
                return false;
            }

            // Compute inverted determinant
            float invDet = 1.0f / det;

            // Compute distance from vertex 0 to the ray origin
            Vec3 tvec = ray.a - vert0;

            // Compute u parameter
            float u = Vec3.dot(tvec, pvec) * invDet;

            // Check if the intersection point is outside the triangle
            if (u < 0.0f || u > 1.0f)
            {
                t = float.MaxValue;
                return false;
            }

            // Compute q vector and v parameter
            Vec3 qvec = Vec3.cross(tvec, edge1);
            float v = Vec3.dot(ray.b, qvec) * invDet;

            // Check if the intersection point is outside the triangle
            if (v < 0.0f || u + v > 1.0f)
            {
                t = float.MaxValue;
                return false;
            }

            // Compute the distance along the ray to the intersection point
            t = Vec3.dot(edge2, qvec) * invDet;

            // Check if the intersection point is behind the ray origin
            if (t < 0.0f)
            {
                t = float.MaxValue;
                return false;
            }

            // Intersection found
            return true;
        }
    }

    public struct Sphere
    {
        public Vec3 center;
        public float radius;
        public Vec3 color;
        public float reflectivity;
        public int id = -1;

        public Sphere(Vec3 color, Vec3 center, float radius, float reflectivity)
        {
            this.color = color;
            this.center = center;
            this.radius = radius;
            this.reflectivity = reflectivity;
        }

        public bool Intersect(Ray ray, out float t)
        {
            t = float.MaxValue;

            Vec3 oc = ray.a - center;
            float a = Vec3.dot(ray.b, ray.b);
            float b = 2.0f * Vec3.dot(oc, ray.b);
            float c = Vec3.dot(oc, oc) - radius * radius;
            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
            {
                // No intersection
                return false;
            }

            float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
            float t1 = (-b - sqrtDiscriminant) / (2 * a);
            float t2 = (-b + sqrtDiscriminant) / (2 * a);

            if (t1 > t2)
            {
                float temp = t1;
                t1 = t2;
                t2 = temp;
            }

            if (t2 < 0)
            {
                // Both intersections are behind the ray's origin
                return false;
            }

            t = (t1 >= 0) ? t1 : t2;

            return true;
        }

        public float intersect(Ray ray)
        {
            return Intersect(ray, out float t) ? t : float.MaxValue;
        }
    }
}
