using GPU.RT;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System.Numerics;

namespace GPU
{
    public struct Camera3D
    {
        public SpecializedValue<int> height { get; set; }
        public SpecializedValue<int> width { get; set; }
        public Vec3 noHitColor { get; set; }

        public float verticalFov { get; set; }

        public Vec3 origin { get; set; }
        public Vec3 lookAt { get; set; }
        public Vec3 up { get; set; }
        public OrthoNormalBasis axis { get; set; }

        public float aspectRatio { get; set; }
        public float cameraPlaneDist { get; set; }
        public float reciprocalHeight { get; set; }
        public float reciprocalWidth { get; set; }

        public bool isBGR { get; set; }
        public float depthCutOff { get; set; }

        public Camera3D(Camera3D camera, Vec3 movement, Vec3 turn)
        {
            this.width = camera.width;
            this.height = camera.height;
            this.isBGR = camera.isBGR;
            this.depthCutOff = camera.depthCutOff;

            this.noHitColor = camera.noHitColor;

            Vector4 temp = camera.lookAt - camera.origin;

            if (turn.y != 0)
            {
                temp += Vector4.Transform(temp, Matrix4x4.CreateFromAxisAngle(Vec3.cross(Vec3.cross(camera.up, (camera.lookAt - camera.origin)), (camera.lookAt - camera.origin)), (float)turn.y));
            }
            if (turn.x != 0)
            {
                temp += Vector4.Transform(temp, Matrix4x4.CreateFromAxisAngle(Vec3.cross(camera.up, (camera.lookAt - camera.origin)), (float)turn.x));
            }

            lookAt = camera.origin + Vec3.unitVector(temp);

            this.origin = camera.origin + movement;
            this.lookAt += movement;
            this.up = camera.up;

            axis = OrthoNormalBasis.fromZY(Vec3.unitVector(lookAt - origin), up);

            aspectRatio = ((float)width / (float)height);
            cameraPlaneDist = 1.0f / XMath.Tan(camera.verticalFov * XMath.PI / 360.0f);
            this.verticalFov = camera.verticalFov;
            reciprocalHeight = 1.0f / height;
            reciprocalWidth = 1.0f / width;
        }

        public Camera3D(Camera3D camera, float vfov)
        {
            this.width = camera.width;
            this.height = camera.height;
            this.noHitColor = camera.noHitColor;
            this.isBGR = camera.isBGR;
            this.depthCutOff = camera.depthCutOff;

            this.verticalFov = vfov;

            this.origin = camera.origin;
            this.lookAt = camera.lookAt;
            this.up = camera.up;

            axis = OrthoNormalBasis.fromZY(Vec3.unitVector(lookAt - origin), up);

            aspectRatio = ((float)width / (float)height);
            cameraPlaneDist = 1.0f / XMath.Tan(verticalFov * XMath.PI / 360.0f);
            reciprocalHeight = 1.0f / height;
            reciprocalWidth = 1.0f / width;
        }

        public Camera3D(Camera3D camera, int width, int height)
        {
            this.width = new SpecializedValue<int>(width);
            this.height = new SpecializedValue<int>(height);
            this.noHitColor = camera.noHitColor;
            this.isBGR = camera.isBGR;
            this.depthCutOff = camera.depthCutOff;

            this.verticalFov = camera.verticalFov;

            this.origin = camera.origin;
            this.lookAt = camera.lookAt;
            this.up = camera.up;

            axis = OrthoNormalBasis.fromZY(Vec3.unitVector(lookAt - origin), up);

            aspectRatio = ((float)width / (float)height);
            cameraPlaneDist = 1.0f / XMath.Tan(verticalFov * XMath.PI / 360.0f);
            reciprocalHeight = 1.0f / height;
            reciprocalWidth = 1.0f / width;
        }

        public Camera3D(Vec3 origin, Vec3 lookAt, Vec3 up, int width, int height, float verticalFov, Vec3 noHitColor, bool isBGR = false)
        {
            this.width = new SpecializedValue<int>(width);
            this.height = new SpecializedValue<int>(height);
            this.noHitColor = noHitColor;
            this.isBGR = isBGR;
            this.depthCutOff = 1;

            this.verticalFov = verticalFov;
            this.origin = origin;
            this.lookAt = lookAt;
            this.up = up;

            axis = OrthoNormalBasis.fromZY(Vec3.unitVector(lookAt - origin), up);

            aspectRatio = ((float)width / (float)height);
            cameraPlaneDist = 1.0f / XMath.Tan(verticalFov * XMath.PI / 360.0f);
            reciprocalHeight = 1.0f / height;
            reciprocalWidth = 1.0f / width;
        }


        private Ray rayFromUnit(float x, float y)
        {
            Vec3 xContrib = axis.x * -x * aspectRatio;
            Vec3 yContrib = axis.y * -y;
            Vec3 zContrib = axis.z * cameraPlaneDist;
            Vec3 direction = Vec3.unitVector(xContrib + yContrib + zContrib);

            return new Ray(origin, direction);
        }

        //public Ray GetRay(float x, float y)
        //{
        //    return rayFromUnit(2f * (x * reciprocalWidth) - 1f, 2f * (y * reciprocalHeight) - 1f);
        //}

        public Ray GetRay(float x, float y)
        {
            return rayFromUnit((2f * x) - 1f, (2f * y) - 1f);
        }
    }

    public struct OrthoNormalBasis
    {
        public Vec3 x { get; set; }
        public Vec3 y { get; set; }
        public Vec3 z { get; set; }

        public OrthoNormalBasis(Vec3 x, Vec3 y, Vec3 z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }


        public Vec3 transform(Vec3 pos)
        {
            return x * pos.x + y * pos.y + z * pos.z;
        }


        public static OrthoNormalBasis fromXY(Vec3 x, Vec3 y)
        {
            Vec3 zz = Vec3.unitVector(Vec3.cross(x, y));
            Vec3 yy = Vec3.unitVector(Vec3.cross(zz, x));
            return new OrthoNormalBasis(x, yy, zz);
        }


        public static OrthoNormalBasis fromYX(Vec3 y, Vec3 x)
        {
            Vec3 zz = Vec3.unitVector(Vec3.cross(x, y));
            Vec3 xx = Vec3.unitVector(Vec3.cross(y, zz));
            return new OrthoNormalBasis(xx, y, zz);
        }


        public static OrthoNormalBasis fromXZ(Vec3 x, Vec3 z)
        {
            Vec3 yy = Vec3.unitVector(Vec3.cross(z, x));
            Vec3 zz = Vec3.unitVector(Vec3.cross(x, yy));
            return new OrthoNormalBasis(x, yy, zz);
        }


        public static OrthoNormalBasis fromZX(Vec3 z, Vec3 x)
        {
            Vec3 yy = Vec3.unitVector(Vec3.cross(z, x));
            Vec3 xx = Vec3.unitVector(Vec3.cross(yy, z));
            return new OrthoNormalBasis(xx, yy, z);
        }


        public static OrthoNormalBasis fromYZ(Vec3 y, Vec3 z)
        {
            Vec3 xx = Vec3.unitVector(Vec3.cross(y, z));
            Vec3 zz = Vec3.unitVector(Vec3.cross(xx, y));
            return new OrthoNormalBasis(xx, y, zz);
        }


        public static OrthoNormalBasis fromZY(Vec3 z, Vec3 y)
        {
            Vec3 xx = Vec3.unitVector(Vec3.cross(y, z));
            Vec3 yy = Vec3.unitVector(Vec3.cross(z, xx));
            return new OrthoNormalBasis(xx, yy, z);
        }


        public static OrthoNormalBasis fromZ(Vec3 z)
        {
            Vec3 xx;
            if (XMath.Abs(Vec3.dot(z, new Vec3(1, 0, 0))) > 0.99999f)
            {
                xx = Vec3.unitVector(Vec3.cross(new Vec3(0, 1, 0), z));
            }
            else
            {
                xx = Vec3.unitVector(Vec3.cross(new Vec3(1, 0, 0), z));
            }
            Vec3 yy = Vec3.unitVector(Vec3.cross(z, xx));
            return new OrthoNormalBasis(xx, yy, z);
        }
    }

    public readonly struct Ray
    {
        // position
        public readonly Vec3 a;
        // direction
        public readonly Vec3 b;

        public Ray(Vec3 a, Vec3 b)
        {
            this.a = a;
            this.b = Vec3.unitVector(b);
        }

        public Vec3 pointAtParameter(float t)
        {
            return a + (t * b);
        }
    }

    public struct AABB
    {
        public Vec3 min;
        public Vec3 max;

        public AABB(Vec3 min, Vec3 max)
        {
            this.min = min;
            this.max = max;
        }

        public AABB(Sphere sphere)
        {
            float radius = sphere.radius;
            Vec3 center = sphere.center;
            min = center - radius;
            max = center + radius;
        }

        public Vec3 Center()
        {
            return (min + max) / 2f;
        }

        public bool Intersect(Ray ray, out float t_max)
        {
            // Compute the inverse direction of the ray
            Vec3 inv_direction = new Vector3(1.0f / ray.b.x, 1.0f / ray.b.y, 1.0f / ray.b.z);

            // Compute the t-values for the intersections of the ray with the slabs of the AABB
            float t_x1 = (min.x - ray.a.x) * inv_direction.x;
            float t_x2 = (max.x - ray.a.x) * inv_direction.x;
            float t_y1 = (min.y - ray.a.y) * inv_direction.y;
            float t_y2 = (max.y - ray.a.y) * inv_direction.y;
            float t_z1 = (min.z - ray.a.z) * inv_direction.z;
            float t_z2 = (max.z - ray.a.z) * inv_direction.z;

            // Compute the t-values for the entry and exit points of the ray in the AABB
            float t_min = XMath.Max(XMath.Max(XMath.Min(t_x1, t_x2), XMath.Min(t_y1, t_y2)), XMath.Min(t_z1, t_z2));
            t_max = XMath.Min(XMath.Min(XMath.Max(t_x1, t_x2), XMath.Max(t_y1, t_y2)), XMath.Max(t_z1, t_z2));

            // Check if there is a valid intersection
            return (t_min < t_max) && (t_min < t_max && t_min < t_max);
        }

        public int GetLargestExtentAxis()
        {
            Vec3 extents = max - min;
            if (extents.x > extents.y && extents.x > extents.z)
            {
                return 0;
            }
            else if (extents.y > extents.z)
            {
                return 1;
            }
            else
            {
                return 2;
            }
        }

        public float SurfaceArea()
        {
            Vec3 d = max - min;
            return 2.0f * (d.x * d.y + d.x * d.z + d.y * d.z);
        }

        public static AABB Merge(AABB a, AABB b)
        {
            Vec3 min = new Vec3(
                XMath.Min(a.min.x, b.min.x),
                XMath.Min(a.min.y, b.min.y),
                XMath.Min(a.min.z, b.min.z));

            Vec3 max = new Vec3(
                XMath.Max(a.max.x, b.max.x),
                XMath.Max(a.max.y, b.max.y),
                XMath.Max(a.max.z, b.max.z));

            return new AABB(min, max);
        }
    }
}
