using GPU;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ILGPUView2.GPU.Filters
{
    public struct FilterDepth
    {
        float depth_width;
        float depth_height;

        ArrayView1D<byte, Stride1D.Dense> depth_frame_1;
        ArrayView1D<byte, Stride1D.Dense> depth_frame_2;
        ArrayView1D<byte, Stride1D.Dense> depth_frame_3;
        ArrayView1D<byte, Stride1D.Dense> depth_frame_4;

        public FilterDepth(float depth_width, float depth_height,
            ArrayView1D<byte, Stride1D.Dense> depth_frame_1, ArrayView1D<byte, Stride1D.Dense> depth_frame_2,
            ArrayView1D<byte, Stride1D.Dense> depth_frame_3, ArrayView1D<byte, Stride1D.Dense> depth_frame_4)
        {
            this.depth_width = depth_width;
            this.depth_height = depth_height;
            this.depth_frame_1 = depth_frame_1;
            this.depth_frame_2 = depth_frame_2;
            this.depth_frame_3 = depth_frame_3;
            this.depth_frame_4 = depth_frame_4;
        }

        public RGBA32 GetDepthAt(ArrayView1D<byte, Stride1D.Dense> frame, float u, float v)
        {
            int index = (int)((u * depth_width + v * depth_height * depth_width) * 4);
            return new RGBA32(frame[index], frame[index + 1], frame[index + 2], frame[index + 3]);
        }

        public void SetDepthAt(ArrayView1D<byte, Stride1D.Dense> frame, float u, float v, RGBA32 depth)
        {
            int index = (int)((u * depth_width + v * depth_height * depth_width) * 4);

            frame[index + 0] = depth.r;
            frame[index + 1] = depth.g;
            frame[index + 2] = depth.b;
            frame[index + 3] = depth.a;
        }

        public RGBA32 depthToRGBA(float depth_val)
        {
            return new RGBA32(depth_val, depth_val, depth_val);
        }

        public void ShuffleDepth(float u, float v, dImage depth_frame_0)
        {
            SetDepthAt(depth_frame_4, u, v, GetDepthAt(depth_frame_3, u, v));
            SetDepthAt(depth_frame_3, u, v, GetDepthAt(depth_frame_2, u, v));
            SetDepthAt(depth_frame_2, u, v, GetDepthAt(depth_frame_1, u, v));
            SetDepthAt(depth_frame_1, u, v, depth_frame_0.GetColorAt(u, v));
        }

        public float GetValue(float u, float v, dImage depth_frame_0, int method)
        {
            switch (method)
            {
                case 0:
                    return GetDepthFrame(depth_frame_0, u, v, 0);
                case 1:
                    return AllDepthFrameTAA(u, v, depth_frame_0);
                case 2:
                    return AllDepthFrameTAA_EMA(u, v, depth_frame_0);
                case 3:
                    return AllDepthFrameTAA_WeightedTemporalAccumulation(u, v, depth_frame_0);
                case 4:
                    return AllDepthFrameTAA_NeighborhoodClamping(u, v, depth_frame_0);
                case 5:
                    return AllDepthFrameTAA_CWTA(u, v, depth_frame_0);
            }

            return AllDepthFrameTAA(u, v, depth_frame_0);
        }

        public RGBA32 Apply(int x, int y, dImage depth_frame_0)
        {
            float d_u = (x / depth_width);
            float d_v = (y / depth_height);

            int taa_method_count = 5;

            // Store the depth values from all TAA methods
            float[] depth_values = new float[taa_method_count];
            for (int i = 1; i <= taa_method_count; i++)
            {
                depth_values[i - 1] = GetValue(d_u, d_v, depth_frame_0, i);
            }

            // Calculate the absolute differences between each pair of depth values
            float[] difference_sums = new float[taa_method_count];
            for (int i = 0; i < taa_method_count; i++)
            {
                for (int j = 0; j < taa_method_count; j++)
                {
                    if (i != j)
                    {
                        difference_sums[i] += XMath.Abs(depth_values[i] - depth_values[j]);
                    }
                }
            }

            // Choose the depth value with the smallest sum of differences
            float filtered_depth = depth_values[0];
            float min_difference_sum = difference_sums[0];

            for (int i = 1; i < taa_method_count; i++)
            {
                if (difference_sums[i] < min_difference_sum)
                {
                    filtered_depth = depth_values[i];
                    min_difference_sum = difference_sums[i];
                }
            }

            // Shuffle the depth frames
            ShuffleDepth(d_u, d_v, depth_frame_0);

            // Return the filtered depth value as an RGBA32 object
            return new RGBA32(filtered_depth, filtered_depth, filtered_depth);
        }


        public float GetDepthFrame(dImage depth_frame_0, float u, float v, int frame)
        {
            switch (frame)
            {
                case 0:
                    return depth_frame_0.GetColorAt(u, v).r / 255.0f;
                case 1:
                    return GetDepthAt(depth_frame_1, u, v).r / 255.0f;
                case 2:
                    return GetDepthAt(depth_frame_2, u, v).r / 255.0f;
                case 3:
                    return GetDepthAt(depth_frame_3, u, v).r / 255.0f;
                case 4:
                    return GetDepthAt(depth_frame_4, u, v).r / 255.0f;
            }
            return 0;
        }

        public float AllDepthFrameTAA_CWTA(float u, float v, dImage depth_frame_0)
        {
            float filtered_depth_val = 0f;

            // Set the initial blending weights for each frame
            float[] weights = { 1, 1, 1, 1, 1 };
            float weight_sum = 5f;
            float clamping_threshold = 5f / 255f; // Adjust this value based on the depth units and scene characteristics

            // Calculate the filtered depth value using clamped weighted temporal accumulation
            for (int i = 0; i < weights.Length - 1; i++)
            {
                float current_frame_val = GetDepthFrame(depth_frame_0, u, v, i);
                float next_frame_val = GetDepthFrame(depth_frame_0, u, v, i + 1);
                float difference = XMath.Abs(current_frame_val - next_frame_val);

                // Handle edge cases like motion or camera movement by using heuristics based on the depth difference
                if (difference > clamping_threshold)
                {
                    weights[i + 1] = 0.5f * weights[i]; // Decrease the weight for the next frame if there's a significant difference
                    weight_sum += (weights[i + 1] - 1); // Update the weight sum
                }

                // Clamp the depth values based on the clamping threshold
                float clamped_depth_val = XMath.Max(0, XMath.Min(current_frame_val, next_frame_val + clamping_threshold));
                clamped_depth_val = XMath.Min(1, XMath.Max(clamped_depth_val, next_frame_val - clamping_threshold));

                filtered_depth_val += clamped_depth_val * weights[i];
            }

            // Add the contribution of the last frame
            filtered_depth_val += GetDepthFrame(depth_frame_0, u, v, 4) * weights[4];

            return filtered_depth_val / weight_sum;
        }

        public float AllDepthFrameTAA_NeighborhoodClamping(float u, float v, dImage depth_frame_0)
        {
            float filtered_depth_val = 0f;

            // Set the blending weights for each frame
            float[] weights = { 1, 1, 1, 1, 1 };

            float current_depth = GetDepthFrame(depth_frame_0, u, v, 0);
            float min_depth = current_depth;
            float max_depth = current_depth;

            // Calculate the min and max depth values in the neighborhood
            for (int i = 1; i < weights.Length; i++)
            {
                float depth_val = GetDepthFrame(depth_frame_0, u, v, i);
                min_depth = XMath.Min(min_depth, depth_val);
                max_depth = XMath.Max(max_depth, depth_val);
            }

            // Clamp the depth values based on the min and max depth values in the neighborhood
            for (int i = 0; i < weights.Length; i++)
            {
                float depth_val = GetDepthFrame(depth_frame_0, u, v, i);
                float clamped_depth_val = XMath.Min(XMath.Max(depth_val, min_depth), max_depth);
                filtered_depth_val += clamped_depth_val * weights[i];
            }

            return filtered_depth_val / 5f;
        }


        public float AllDepthFrameTAA_WeightedTemporalAccumulation(float u, float v, dImage depth_frame_0)
        {
            float filtered_depth_val = 0f;

            // Set the initial blending weights for each frame
            float[] weights = { 1, 1, 1, 1, 1 };
            float weight_sum = 5f;

            // Calculate the filtered depth value using weighted temporal accumulation
            for (int i = 0; i < weights.Length - 1; i++)
            {
                float current_frame_val = GetDepthFrame(depth_frame_0, u, v, i);
                float next_frame_val = GetDepthFrame(depth_frame_0, u, v, i + 1);
                float difference = XMath.Abs(current_frame_val - next_frame_val);

                // Handle edge cases like motion or camera movement by using heuristics based on the depth difference
                if (difference > 5f / 255f) // Adjust this value based on the depth units and scene characteristics
                {
                    weights[i + 1] = 0.5f * weights[i]; // Decrease the weight for the next frame if there's a significant difference
                    weight_sum += (weights[i + 1] - 1); // Update the weight sum
                }

                filtered_depth_val += current_frame_val * weights[i];
            }

            // Add the contribution of the last frame
            filtered_depth_val += GetDepthFrame(depth_frame_0, u, v, 4) * weights[4];

            return filtered_depth_val / weight_sum;
        }

        public float AllDepthFrameTAA_EMA(float u, float v, dImage depth_frame_0)
        {
            float filtered_depth_val = 0f;
            float alpha = 0.1f; // EMA blending factor, adjust this value to control the contribution of recent frames.

            // Calculate the filtered depth value using Exponential Moving Average (EMA) of depth frames
            float previous_frame_val = GetDepthFrame(depth_frame_0, u, v, 0);
            filtered_depth_val = previous_frame_val;

            for (int i = 1; i < 5; i++)
            {
                float current_frame_val = GetDepthFrame(depth_frame_0, u, v, i);
                float difference = XMath.Abs(current_frame_val - previous_frame_val);

                // Handle edge cases like motion or camera movement by using heuristics based on the depth difference
                if (difference > 5f / 255f) // Adjust this value based on the depth units and scene characteristics
                {
                    alpha = 0.5f; // Increase the blending factor for larger differences to give more importance to recent frames
                }

                filtered_depth_val = alpha * current_frame_val + (1 - alpha) * filtered_depth_val;
                previous_frame_val = current_frame_val;
            }

            return filtered_depth_val;
        }


        public float AllDepthFrameTAA(float u, float v, dImage depth_frame_0)
        {
            float filtered_depth_val = 0f;
            // Set the blending weights for each frame
            float[] weights = { 1, 1, 1, 1, 1 };

            // Calculate the filtered depth value using a weighted blend of depth frames
            for (int i = 0; i < weights.Length; i++)
            {
                filtered_depth_val += GetDepthFrame(depth_frame_0, u, v, i) * weights[i];
            }

            return filtered_depth_val / 5f;
        }
    }
}