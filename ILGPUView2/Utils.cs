using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using ILGPU.Algorithms;

namespace GPU
{
    // ChatGPT wrote this, its VERY slow
    public struct NoiseGenerator
    {
        const int octaves = 6;
        const float persistence = 0.5f;
        const float initialAmp = 1.0f;

        int tick;

        public NoiseGenerator(int tick)
        {
            this.tick = tick;
        }

        private static float lerp(float a, float b, float t)
        {
            return (1f - t) * a + t * b;
        }

        public float SmoothStep(float edge0, float edge1, float x)
        {
            // Clamp the input x value to the range [0, 1]
            float t = XMath.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);

            // Calculate the smoothstep function value
            return t * t * (3f - 2f * t);
        }

        public Vec2 GetRandomVector(Vec2 v)
        {
            // Use the tick value to seed the random number generator
            int seed = tick;

            // Re-seed the random number generator with the cell position to ensure repeatable results for each cell
            seed = (int)(seed + v.x * 127 + v.y * 311) * 16807 % 2147483647;

            // Shuffle the random number generator's internal state to add additional randomness
            for (int i = 0; i < tick % 10; i++)
            {
                seed = (seed * 1103515245 + 12345) % 2147483647;
            }

            // Use the re-seeded and shuffled random number generator to generate a random vector for the given cell position
            float x = ((seed >> 16) & 0x7FFF) / (float)0x7FFF;
            float y = ((seed >> 1) & 0x7FFF) / (float)0x7FFF;

            return new Vec2(x, y);
        }

        public Vec3 GetRandomVector(Vec3 v)
        {
            // Use the tick value to seed the random number generator
            int seed = tick;

            // Re-seed the random number generator with the cell position to ensure repeatable results for each cell
            seed = (int)(seed + v.x * 127 + v.y * 311 + v.z * 523) * 16807 % 2147483647;

            // Shuffle the random number generator's internal state to add additional randomness
            for (int i = 0; i < tick % 10; i++)
            {
                seed = (seed * 1103515245 + 12345) % 2147483647;
            }

            // Use the re-seeded and shuffled random number generator to generate a random vector for the given cell position
            float x = ((seed >> 16) & 0x7FFF) / (float)0x7FFF;
            float y = ((seed >> 1) & 0x7FFF) / (float)0x7FFF;
            float z = ((seed >> 11) & 0x7FFF) / (float)0x7FFF;

            return new Vec3(x, y, z);
        }

        public float GenerateNoise(float x, float y)
        {
            // Define the dimensions of the sequence
            const int sequenceWidth = 8;
            const int sequenceHeight = 8;

            // Calculate the coordinates of the cell in the sequence that contains the input point
            int cellX = (int)x % sequenceWidth;
            int cellY = (int)y % sequenceHeight;

            // Calculate the fractional distance from the input point to the upper left corner of the cell
            float dx = x - XMath.Floor(x);
            float dy = y - XMath.Floor(y);

            // Get the four corner points of the cell
            Vec2 corner1 = new Vec2(cellX, cellY);
            Vec2 corner2 = new Vec2(cellX + 1, cellY);
            Vec2 corner3 = new Vec2(cellX, cellY + 1);
            Vec2 corner4 = new Vec2(cellX + 1, cellY + 1);

            // Generate random vectors for each corner point
            Vec2 vector1 = GetRandomVector(corner1);
            Vec2 vector2 = GetRandomVector(corner2);
            Vec2 vector3 = GetRandomVector(corner3);
            Vec2 vector4 = GetRandomVector(corner4);

            // Calculate the dot product between the distance vectors and the random vectors for each corner
            float dot1 = Vec2.Dot(new Vec2(dx, dy), vector1);
            float dot2 = Vec2.Dot(new Vec2(dx - 1, dy), vector2);
            float dot3 = Vec2.Dot(new Vec2(dx, dy - 1), vector3);
            float dot4 = Vec2.Dot(new Vec2(dx - 1, dy - 1), vector4);

            // Interpolate between the dot products using a smoothstep function
            float smoothstepX = SmoothStep(0f, 1f, dx);
            float smoothstepY = SmoothStep(0f, 1f, dy);
            float topInterp = lerp(dot1, dot2, smoothstepX);
            float bottomInterp = lerp(dot3, dot4, smoothstepX);
            float interp = lerp(topInterp, bottomInterp, smoothstepY);

            return interp;
        }

        public float GenerateNoise(float x, float y, float z)
        {
            // Define the dimensions of the sequence
            const int sequenceWidth = 8;
            const int sequenceHeight = 8;
            const int sequenceDepth = 8;

            // Calculate the coordinates of the cell in the sequence that contains the input point
            int cellX = (int)x % sequenceWidth;
            int cellY = (int)y % sequenceHeight;
            int cellZ = (int)z % sequenceDepth;

            // Calculate the fractional distance from the input point to the upper left corner of the cell
            float dx = x - XMath.Floor(x);
            float dy = y - XMath.Floor(y);
            float dz = z - XMath.Floor(z);

            // Get the eight corner points of the cell
            Vec3 corner1 = new Vec3(cellX, cellY, cellZ);
            Vec3 corner2 = new Vec3(cellX + 1, cellY, cellZ);
            Vec3 corner3 = new Vec3(cellX, cellY + 1, cellZ);
            Vec3 corner4 = new Vec3(cellX + 1, cellY + 1, cellZ);
            Vec3 corner5 = new Vec3(cellX, cellY, cellZ + 1);
            Vec3 corner6 = new Vec3(cellX + 1, cellY, cellZ + 1);
            Vec3 corner7 = new Vec3(cellX, cellY + 1, cellZ + 1);
            Vec3 corner8 = new Vec3(cellX + 1, cellY + 1, cellZ + 1);

            // Generate random vectors for each corner point
            Vec3 vector1 = GetRandomVector(corner1);
            Vec3 vector2 = GetRandomVector(corner2);
            Vec3 vector3 = GetRandomVector(corner3);
            Vec3 vector4 = GetRandomVector(corner4);
            Vec3 vector5 = GetRandomVector(corner5);
            Vec3 vector6 = GetRandomVector(corner6);
            Vec3 vector7 = GetRandomVector(corner7);
            Vec3 vector8 = GetRandomVector(corner8);

            // Calculate the dot product between the distance vectors and the random vectors for each corner
            float dot1 = Vec3.dot(new Vec3(dx, dy, dz), vector1);
            float dot2 = Vec3.dot(new Vec3(dx - 1, dy, dz), vector2);
            float dot3 = Vec3.dot(new Vec3(dx, dy - 1, dz), vector3);
            float dot4 = Vec3.dot(new Vec3(dx - 1, dy - 1, dz), vector4);
            float dot5 = Vec3.dot(new Vec3(dx, dy, dz - 1), vector5);
            float dot6 = Vec3.dot(new Vec3(dx - 1, dy, dz - 1), vector6);
            float dot7 = Vec3.dot(new Vec3(dx, dy - 1, dz - 1), vector7);
            float dot8 = Vec3.dot(new Vec3(dx - 1, dy - 1, dz - 1), vector8);

            // Interpolate between the dot products using a trilinear interpolation function
            float smoothstepX = SmoothStep(0, 1, dx);
            float smoothstepY = SmoothStep(0, 1, dy);
            float smoothstepZ = SmoothStep(0, 1, dz);

            float topFront = (float)(dot1 * (1 - smoothstepX) + dot2 * smoothstepX);
            float bottomFront = (float)(dot3 * (1 - smoothstepX) + dot4 * smoothstepX);
            float topBack = (float)(dot5 * (1 - smoothstepX) + dot6 * smoothstepX);
            float bottomBack = (float)(dot7 * (1 - smoothstepX) + dot8 * smoothstepX);
            float front = (float)(topFront * (1 - smoothstepY) + bottomFront * smoothstepY);
            float back = (float)(topBack * (1 - smoothstepY) + bottomBack * smoothstepY);

            return (float)(front * (1 - smoothstepZ) + back * smoothstepZ);
        }

        public float GeneratePerlin(Vec3 pos)
        {
            // Define the scale and amplitude of the initial octave
            float scale = 1.0f;
            float amplitude = initialAmp;

            // Initialize the total value and the maximum possible value
            float total = 0.0f;
            float maxPossibleValue = 0.0f;

            // Loop through each octave and add to the total value
            for (int i = 0; i < octaves; i++)
            {
                // Calculate the frequency and amplitude of the current octave
                float frequency = XMath.Pow(2, i);
                frequency /= scale; // adjust frequency based on scale
                amplitude *= persistence;

                // Calculate the contribution of the current octave to the total value
                float noiseValue = GenerateNoise(pos.x * frequency, pos.y * frequency, pos.z * frequency) * amplitude;
                total += noiseValue;

                // Add to the maximum possible value based on the amplitude of the current octave
                maxPossibleValue += amplitude;
            }

            // Normalize the total value to the range [-1, 1] and return it
            return total / maxPossibleValue;
        }

        public float GeneratePerlin(float x)
        {
            // Define the scale and amplitude of the initial octave
            float amplitude = initialAmp;

            // Initialize the total value and the maximum possible value
            float total = 0.0f;
            float maxPossibleValue = 0.0f;

            // Loop through each octave and add to the total value
            for (int i = 0; i < octaves; i++)
            {
                // Calculate the frequency and amplitude of the current octave
                float frequency = XMath.Pow(2, i);
                amplitude *= persistence;

                // Calculate the contribution of the current octave to the total value
                float noiseValue = GenerateNoise(x * frequency, 0f) * amplitude;
                total += noiseValue;

                // Add to the maximum possible value based on the amplitude of the current octave
                maxPossibleValue += amplitude;
            }

            // Normalize the total value to the range [-1, 1] and return it
            return total / maxPossibleValue;
        }
    }

    public static class Utils
    {
        public static bool isLessThanWithin(float val, float threshold, float range)
        {
            return (val < threshold + range);
        }

        public static bool isGreaterThanWithin(float val, float threshold, float range)
        {
            return (val > threshold - range);
        }

        public static bool isLessThanEqualWithin(float val, float threshold, float range)
        {
            return (val <= threshold + range);
        }

        public static bool isGreaterThanEqualWithin(float val, float threshold, float range)
        {
            return (val >= threshold - range);
        }

        public static bool isWithin(float val, float range)
        {
            return (val >= -range && val <= range);
        }

        public static float GetRandom(uint seed, float min, float max)
        {
            int intRange = int.MaxValue;
            float range = max - min;

            // Get a random integer in the range [0, int.MaxValue]
            int randomInt = GetRandomInt(seed, 0, int.MaxValue);

            // Scale the random integer to the range [0, 1]
            float scaled = (float)randomInt / intRange;

            // Scale the random float to the desired range
            return min + scaled * range;
        }

        public static int GetRandomInt(uint seed, int min, int max)
        {
            // Ensure that min is less than or equal to max
            if (min > max)
            {
                int temp = min;
                min = max;
                max = temp;
            }

            // Implement a simple linear congruential generator
            // Source: https://en.wikipedia.org/wiki/Linear_congruential_generator
            const int a = 1664525;
            const int c = 1013904223;
            const int m = int.MaxValue;

            // Compute the scaled random number
            float x = seed;
            x = (a * x + c) % m;
            float range = max - min + 1;
            int scaled = (int)(x % range);

            // Add the minimum value to the scaled random number
            int toReturn = min + scaled;
            return toReturn;
        }

        public static uint CreateSeed(uint tick, uint counter, float x, float y)
        {
            const uint FNV_PRIME = 16777619;
            const uint FNV_OFFSET_BASIS = 2166136261;

            // Convert x and y to integer values in the range [0, 2^32-1]
            uint ix = (uint)(x * 4294967295);
            uint iy = (uint)(y * 4294967295);

            // Compute FNV-1a hash of the input values
            uint hash = FNV_OFFSET_BASIS;
            hash ^= tick;
            hash *= FNV_PRIME;
            hash ^= counter;
            hash *= FNV_PRIME;
            hash ^= ix;
            hash *= FNV_PRIME;
            hash ^= iy;
            hash *= FNV_PRIME;

            return hash;
        }

        public static List<string> getSegments(string input, int segmentLength)
        {
            int numSegments = (input.Length + segmentLength - 1) / segmentLength;
            List<string> segmentList = new List<string>(numSegments);

            for (int i = 0; i < numSegments - 1; i++)
            {
                int startIndex = i * segmentLength;
                int length = segmentLength;
                segmentList.Add(input.Substring(startIndex, length));
            }

            segmentList.Add(input.Substring((numSegments - 1) * segmentLength));

            return segmentList;
        }

        public static (byte[] a, ushort[] b) DecodeByteArrayAndUshortArray(string input)
        {
            string[] parts = input.Split(',');
            byte[] byteArray = Convert.FromBase64String(parts[0]);
            ushort[] ushortArray = FromBase64String(parts[1]);
            return (byteArray, ushortArray);
        }

        public static ushort[] FromBase64String(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            ushort[] data = new ushort[bytes.Length / sizeof(ushort)];
            Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
            return data;
        }

        public static string EncodeByteArrayAndUshortArray(byte[] byteArray, ushort[] ushortArray)
        {
            string byteArrayBase64 = Convert.ToBase64String(byteArray);
            string ushortArrayBase64 = ToBase64String(ushortArray);
            return byteArrayBase64 + "," + ushortArrayBase64;
        }

        public static string ToBase64String(Span<ushort> data)
        {
            byte[] bytes = new byte[data.Length * sizeof(ushort)];
            MemoryMarshal.AsBytes(data).CopyTo(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static string ToBase64String(ushort[] data)
        {
            byte[] bytes = new byte[data.Length * sizeof(ushort)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }


        public static float Remap(float source, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
        {
            return targetFrom + (source - sourceFrom) * (targetTo - targetFrom) / (sourceTo - sourceFrom);
        }

        public static byte[] BitmapToBytes(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            int size = bitmapData.Width * bitmap.Height * 4;
            byte[] data = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, size);

            bitmap.UnlockBits(bitmapData);
            return data;
        }

        public static Bitmap BitmapFromBytes(byte[] data, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);

            bmp.UnlockBits(bmpData);

            return bmp;
        }

        public static float GetRandomFloat(float v, uint seed0, float seed1, float seed2, uint counter)
        {
            uint seed = CreateSeed(seed0, counter, seed1, seed2);
            return GetRandom(seed, -v, v);
        }
    }
}
