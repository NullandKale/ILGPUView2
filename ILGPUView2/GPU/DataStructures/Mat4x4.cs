using GPU;
using ILGPU.Algorithms;
using ILGPU.IR;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPU
{
    public struct Mat4x4
    {
        public float data_0_0, data_0_1, data_0_2, data_0_3,
                     data_1_0, data_1_1, data_1_2, data_1_3,
                     data_2_0, data_2_1, data_2_2, data_2_3,
                     data_3_0, data_3_1, data_3_2, data_3_3;

        public Mat4x4(float diagonal = 1.0f)
        {
            data_0_0 = diagonal; data_0_1 = 0; data_0_2 = 0; data_0_3 = 0;
            data_1_0 = 0; data_1_1 = diagonal; data_1_2 = 0; data_1_3 = 0;
            data_2_0 = 0; data_2_1 = 0; data_2_2 = diagonal; data_2_3 = 0;
            data_3_0 = 0; data_3_1 = 0; data_3_2 = 0; data_3_3 = diagonal;

            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    System.Diagnostics.Debug.Assert(!float.IsNaN(get(i, j)));
                }
            }
        }

        public Mat4x4(float[] values)
        {
            if (values.Length != 16)
            {
                throw new ArgumentException("Input array must have exactly 16 elements.");
            }

            data_0_0 = values[0];
            data_0_1 = values[1];
            data_0_2 = values[2];
            data_0_3 = values[3];

            data_1_0 = values[4];
            data_1_1 = values[5];
            data_1_2 = values[6];
            data_1_3 = values[7];

            data_2_0 = values[8];
            data_2_1 = values[9];
            data_2_2 = values[10];
            data_2_3 = values[11];

            data_3_0 = values[12];
            data_3_1 = values[13];
            data_3_2 = values[14];
            data_3_3 = values[15];

            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    System.Diagnostics.Debug.Assert(!float.IsNaN(get(i, j)));
                }
            }
        }


        public float get(int index)
        {
            float val;

            switch (index)
            {
                case 0: val = data_0_0; break;
                case 1: val = data_0_1; break;
                case 2: val = data_0_2; break;
                case 3: val = data_0_3; break;
                case 4: val = data_1_0; break;
                case 5: val = data_1_1; break;
                case 6: val = data_1_2; break;
                case 7: val = data_1_3; break;
                case 8: val = data_2_0; break;
                case 9: val = data_2_1; break;
                case 10: val = data_2_2; break;
                case 11: val = data_2_3; break;
                case 12: val = data_3_0; break;
                case 13: val = data_3_1; break;
                case 14: val = data_3_2; break;
                case 15: val = data_3_3; break;
                default: val = 0; break;
            }

            System.Diagnostics.Debug.Assert(!float.IsNaN(val));
            System.Diagnostics.Debug.Assert(!float.IsInfinity(val));

            return val;
        }

        public void set(int index, float value)
        {
            System.Diagnostics.Debug.Assert(!float.IsNaN(value));
            System.Diagnostics.Debug.Assert(!float.IsInfinity(value));

            switch (index)
            {
                case 0: data_0_0 = value; break;
                case 1: data_0_1 = value; break;
                case 2: data_0_2 = value; break;
                case 3: data_0_3 = value; break;
                case 4: data_1_0 = value; break;
                case 5: data_1_1 = value; break;
                case 6: data_1_2 = value; break;
                case 7: data_1_3 = value; break;
                case 8: data_2_0 = value; break;
                case 9: data_2_1 = value; break;
                case 10: data_2_2 = value; break;
                case 11: data_2_3 = value; break;
                case 12: data_3_0 = value; break;
                case 13: data_3_1 = value; break;
                case 14: data_3_2 = value; break;
                case 15: data_3_3 = value; break;
                default: return;
            }
        }

        public float get(int row, int col)
        {
            int index = row * 4 + col;
            return get(index);
        }

        public void set(int row, int col, float value)
        {
            int index = row * 4 + col;
            set(index, value);
        }

        public static Mat4x4 lookAt(Vec3 eye, Vec3 target, Vec3 up)
        {
            Vec3 zAxis = Vec3.normalize(eye - target);
            Vec3 xAxis = Vec3.normalize(Vec3.cross(up, zAxis));
            Vec3 yAxis = Vec3.cross(zAxis, xAxis);

            Mat4x4 viewMatrix = new Mat4x4();

            viewMatrix.set(0, xAxis.x);
            viewMatrix.set(1, yAxis.x);
            viewMatrix.set(2, zAxis.x);
            viewMatrix.set(3, 0);
            viewMatrix.set(4, xAxis.y);
            viewMatrix.set(5, yAxis.y);
            viewMatrix.set(6, zAxis.y);
            viewMatrix.set(7, 0);
            viewMatrix.set(8, xAxis.z);
            viewMatrix.set(9, yAxis.z);
            viewMatrix.set(10, zAxis.z);
            viewMatrix.set(11, 0);
            viewMatrix.set(12,-Vec3.dot(xAxis, eye));
            viewMatrix.set(13, -Vec3.dot(yAxis, eye));
            viewMatrix.set(14, -Vec3.dot(zAxis, eye));
            viewMatrix.set(15, 1);

            for (int i = 0; i < 16; ++i)
            {
                System.Diagnostics.Debug.Assert(!float.IsNaN(viewMatrix.get(i)));
            }

            return viewMatrix;
        }

        public static Mat4x4 perspective(float fovY, float aspectRatio, float nearPlane, float farPlane)
        {
            float f = 1.0f / (float)Math.Tan(fovY * 0.5f);

            Mat4x4 projectionMatrix = new Mat4x4();

            projectionMatrix.set(0, f / aspectRatio);
            projectionMatrix.set(1, 0);
            projectionMatrix.set(2, 0);
            projectionMatrix.set(3, 0);
            projectionMatrix.set(4, 0)  ;
            projectionMatrix.set(5, f);
            projectionMatrix.set(6, 0);
            projectionMatrix.set(7, 0);
            projectionMatrix.set(8, 0);
            projectionMatrix.set(9, 0);
            projectionMatrix.set(10, -farPlane / (nearPlane - farPlane));
            projectionMatrix.set(11, -1);
            projectionMatrix.set(12, 0);
            projectionMatrix.set(13, 0);
            projectionMatrix.set(14, (nearPlane * farPlane) / (nearPlane - farPlane));
            projectionMatrix.set(15, 0);

            for (int i = 0; i < 16; ++i)
            {
                System.Diagnostics.Debug.Assert(!float.IsNaN(projectionMatrix.get(i)));
            }

            return projectionMatrix;
        }

        public void Copy(Mat4x4 other)
        {
            for (int i = 0; i < 16; ++i)
            {
                set(i, other.get(i));
            }

            for (int i = 0; i < 16; ++i)
            {
                System.Diagnostics.Debug.Assert(!float.IsNaN(get(i)));
            }
        }

        public static Mat4x4 GetViewProjectionMatrix(Camera3D camera)
        {
            // Calculate the view matrix
            Mat4x4 viewMatrix = Mat4x4.lookAt(camera.origin, camera.lookAt, camera.up);

            // Calculate the projection matrix
            float nearPlane = 0.01f;
            float farPlane = 100f;
            float aspectRatio = camera.aspectRatio;
            float verticalFov = camera.verticalFov;
            Mat4x4 projectionMatrix = Mat4x4.perspective(verticalFov, aspectRatio, nearPlane, farPlane);

            // Calculate the view-projection matrix
            Mat4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;

            return viewProjectionMatrix;
        }
        public static void Test_CreateTRS()
        {
            // Test CreateTRS with default parameters
            Mat4x4 mat = Mat4x4.CreateTRS(new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(1, 1, 1));

            // Identity matrix values
            float[] correct_values =
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1,
            };

            Trace.WriteLine("TRS matrix:");

            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    int index = i * 4 + j;
                    // Check correct_values[] vs mat.elements[]
                    // fails at index 0
                    System.Diagnostics.Debug.Assert(mat.get(index) != correct_values[index], $"Element at index {index} is incorrect.");
                    Trace.Write($"{mat.get(index):F2}\t");
                }
                Trace.WriteLine("");
            }
        }

        public static Mat4x4 CreateTRS(Vec3 translation, Vec3 rotation, Vec3 scale)
        {
            // Initialize identity matrix
            Mat4x4 result = new Mat4x4();

            // Create translation matrix
            Mat4x4 translationMat = new Mat4x4();
            translationMat.set(12, translation.x);
            translationMat.set(13, translation.y);
            translationMat.set(14, translation.z);

            // Create rotation matrix
            Mat4x4 rotationMat = Mat4x4.FromEulerAngles(rotation);

            // Create scale matrix
            Mat4x4 scaleMat = new Mat4x4();
            scaleMat.set(0, scale.x);
            scaleMat.set(5, scale.y);
            scaleMat.set(10, scale.z);

            // Combine the matrices
            result = translationMat * rotationMat * scaleMat;

            for (int i = 0; i < 16; ++i)
            {
                System.Diagnostics.Debug.Assert(!float.IsNaN(result.get(i)));
            }

            return result;
        }

        public static Mat4x4 FromEulerAngles(Vec3 rotation)
        {
            float cosX = XMath.Cos(rotation.x * XMath.PI / 180f);
            float sinX = XMath.Sin(rotation.x * XMath.PI / 180f);
            float cosY = XMath.Cos(rotation.y * XMath.PI / 180f);
            float sinY = XMath.Sin(rotation.y * XMath.PI / 180f);
            float cosZ = XMath.Cos(rotation.z * XMath.PI / 180f);
            float sinZ = XMath.Sin(rotation.z * XMath.PI / 180f);

            Mat4x4 rotationMat = new Mat4x4();
            rotationMat.set(0, cosY * cosZ);
            rotationMat.set(1, cosY * sinZ);
            rotationMat.set(2, -sinY);

            rotationMat.set(4, sinX * sinY * cosZ - cosX * sinZ);
            rotationMat.set(5, sinX * sinY * sinZ + cosX * cosZ);
            rotationMat.set(6, sinX * cosY);

            rotationMat.set(8, cosX * sinY * cosZ + sinX * sinZ);
            rotationMat.set(9, cosX * sinY * sinZ - sinX * cosZ);
            rotationMat.set(10, cosX * cosY);

            return rotationMat;
        }

        public static bool CheckNan(params Mat4x4[] mats)
        {
            float sum = 0;
            for (int m = 0; m < mats.Length; m++)
            {
                Mat4x4 mat = mats[m];
                for (int i = 0; i < 16; ++i)
                {
                    float value = mat.get(i);
                    sum += value;
                }
            }

            System.Diagnostics.Debug.Assert(!float.IsNaN(sum));

            return float.IsNaN(sum);
        }

        public static Mat4x4 operator *(Mat4x4 a, Mat4x4 b)
        {
            Mat4x4 result = new Mat4x4(0);

            // checks that all of these mats are not nan
            CheckNan(a, b, result); 

            for (int i = 0; i < 16; ++i)
            {
                int row = i / 4;
                int col = i % 4;

                float value = 0;
                for (int k = 0; k < 4; ++k)
                {
                    int aIndex = row * 4 + k;
                    int bIndex = k * 4 + col;

                    // get is nan checked so it cannot be nan
                    float terma = a.get(aIndex);
                    System.Diagnostics.Debug.Assert(!float.IsNaN(terma));
                    float termb = b.get(bIndex);
                    System.Diagnostics.Debug.Assert(!float.IsNaN(termb));
                    float term = terma * termb;
                    System.Diagnostics.Debug.Assert(!float.IsNaN(term));
                    value += term;
                }

                int resultIndex = row * 4 + col;
                // set checks for nan, and value is nan here
                System.Diagnostics.Debug.Assert(!float.IsNaN(value));
                result.set(resultIndex, value);
            }

            return result;
        }


        public static Vec3 operator *(Mat4x4 a, Vec3 b)
        {
            float x = a.get(0, 0) * b.x + a.get(0, 1) * b.y + a.get(0, 2) * b.z + a.get(0, 3);
            float y = a.get(1, 0) * b.x + a.get(1, 1) * b.y + a.get(1, 2) * b.z + a.get(1, 3);
            float z = a.get(2, 0) * b.x + a.get(2, 1) * b.y + a.get(2, 2) * b.z + a.get(2, 3);
            float w = a.get(3, 0) * b.x + a.get(3, 1) * b.y + a.get(3, 2) * b.z + a.get(3, 3);

            System.Diagnostics.Debug.Assert(!float.IsNaN(x));
            System.Diagnostics.Debug.Assert(!float.IsNaN(y));
            System.Diagnostics.Debug.Assert(!float.IsNaN(z));
            System.Diagnostics.Debug.Assert(!float.IsNaN(w));

            return new Vec3(x / w, y / w, z / w);
        }


        public Vec3 GetTranslation()
        {
            Vec3 translation = new Vec3(get(12), get(13), get(14));
            return translation;
        }

        public Vec3 GetRotation()
        {
            float s;
            float yaw, pitch, roll;
            Vec3 shear;

            // Extract scale
            Vec3 xAxis = new Vec3(get(0), get(1), get(2));
            Vec3 yAxis = new Vec3(get(4), get(5), get(6));
            Vec3 zAxis = new Vec3(get(8), get(9), get(10));

            // Normalize axis vectors
            xAxis = Vec3.Normalize(xAxis);
            yAxis = Vec3.Normalize(yAxis);
            zAxis = Vec3.Normalize(zAxis);

            // Extract shear
            s = Vec3.dot(xAxis, yAxis);
            shear = new Vec3(Vec3.dot(xAxis, zAxis), Vec3.dot(yAxis, zAxis), s);

            // Remove the shear from the axes
            xAxis = xAxis - shear * s;
            yAxis = yAxis - shear * Vec3.dot(yAxis, zAxis);

            // Extract rotation
            yaw = XMath.Atan2(yAxis.z, zAxis.z);
            pitch = XMath.Atan2(-xAxis.z, XMath.Sqrt(yAxis.z * yAxis.z + zAxis.z * zAxis.z));
            roll = XMath.Atan2(yAxis.x, xAxis.x);

            // Convert radians to degrees
            yaw *= XMath.PI / 180f;
            pitch *= XMath.PI / 180f;
            roll *= XMath.PI / 180f;

            // Build the output vector
            return new Vec3(pitch, yaw, roll);
        }

        public Vec3 GetScale()
        {
            Vec3 xAxis = new Vec3(get(0), get(1), get(2));
            Vec3 yAxis = new Vec3(get(4), get(5), get(6));
            Vec3 zAxis = new Vec3(get(8), get(9), get(10));

            float scaleX = xAxis.magnitude();
            float scaleY = yAxis.magnitude();
            float scaleZ = zAxis.magnitude();

            return new Vec3(scaleX, scaleY, scaleZ);
        }

        public override string ToString()
        {
            // Extract translation
            Vec3 translation = new Vec3(get(12), get(13), get(14));

            // Extract scale
            Vec3 xAxis = new Vec3(get(0), get(1), get(2));
            Vec3 yAxis = new Vec3(get(4), get(5), get(6));
            Vec3 zAxis = new Vec3(get(8), get(9), get(10));
            Vec3 scale = new Vec3(xAxis.magnitude(), yAxis.magnitude(), zAxis.magnitude());

            // Normalize axis vectors
            xAxis = Vec3.Normalize(xAxis);
            yAxis = Vec3.Normalize(yAxis);
            zAxis = Vec3.Normalize(zAxis);

            // Extract rotation
            float xRot = XMath.Atan2(yAxis.z, zAxis.z);
            float yRot = XMath.Atan2(-xAxis.z, XMath.Sqrt(yAxis.z * yAxis.z + zAxis.z * zAxis.z));
            float zRot = XMath.Atan2(xAxis.y, xAxis.x);

            // Convert radians to degrees
            xRot *= XMath.PI / 180f;
            yRot *= XMath.PI / 180f;
            zRot *= XMath.PI / 180f;

            // Build the output string
            return $"Translation: {translation}, Rotation: ({xRot}, {yRot}, {zRot}), Scale: {scale}";
        }
        public Mat4x4 inverse()
        {
            float[] mat = new float[16];
            float[] inv = new float[16];
            for (int i = 0; i < 16; ++i)
            {
                mat[i] = get(i / 4, i % 4);
            }

            for (int i = 0; i < 16; ++i)
            {
                inv[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            }

            for (int i = 0; i < 4; ++i)
            {
                int pivot = i * 4 + i;
                float pivotValue = mat[pivot];

                for (int j = i + 1; j < 4; ++j)
                {
                    int row = j * 4;
                    float factor = mat[row + i] / pivotValue;

                    for (int k = i; k < 4; ++k)
                    {
                        mat[row + k] -= factor * mat[i * 4 + k];
                        inv[row + k] -= factor * inv[i * 4 + k];
                    }
                }
            }

            for (int i = 3; i >= 0; --i)
            {
                int pivot = i * 4 + i;
                float pivotValue = mat[pivot];

                for (int j = i - 1; j >= 0; --j)
                {
                    int row = j * 4;
                    float factor = mat[row + i] / pivotValue;

                    for (int k = i; k >= 0; --k)
                    {
                        mat[row + k] -= factor * mat[pivot - i + k];
                        inv[row + k] -= factor * inv[pivot - i + k];
                    }
                }

                for (int k = 0; k < 4; ++k)
                {
                    inv[pivot - i + k] /= pivotValue;
                }
            }

            return new Mat4x4(inv);
        }


    }
}

