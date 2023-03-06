using GPU;
using GPU.RT;
using ILGPU;
using ILGPU.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace ILGPUView2.GPU.RT
{
    public class HOST_BVH
    {
        List<node> bvh_nodes;
        List<Sphere> spheres;

        MemoryBuffer1D<node, Stride1D.Dense> device_nodes;
        MemoryBuffer1D<Sphere, Stride1D.Dense> device_spheres;

        public DEVICE_BVH ToDevice(Accelerator device)
        {
            if (bvh_nodes == null)
            {
                return new DEVICE_BVH();
            }

            if (device_nodes != null)
            {
                device_nodes.Dispose();
            }

            if (device_spheres != null)
            {
                device_spheres.Dispose();
            }

            device_nodes = device.Allocate1D(bvh_nodes.ToArray());
            device_spheres = device.Allocate1D(spheres.ToArray());

            return new DEVICE_BVH(device_nodes, device_spheres);
        }

        public int BuildBVH(List<Sphere> objects)
        {
            // Compute the bounding box for the set of objects
            AABB bounding_box = ComputeBoundingBox(objects);

            // Recursively build the BVH
            spheres = objects;

            for (int i = 0; i < spheres.Count; i++)
            {
                Sphere s = objects[i];
                s.id = i;
                objects[i] = s;
            }

            bvh_nodes = new List<node>();
            BuildBVHNode(objects, bounding_box, ref bvh_nodes);

            return bvh_nodes.Count - 1;
        }

        private List<node> BuildBVHNode(List<Sphere> objects, AABB bounding_box, ref List<node> nodes)
        {
            if (objects.Count <= node.child_count)
            {
                // Create a new internal node
                node internal_node = new node { self_isLeaf = 1, aabb = bounding_box, childrenCount = objects.Count };

                // Add child nodes for each object in the set
                for (int i = 0; i < objects.Count; i++)
                {
                    internal_node.SetChild(i, objects[i].id);
                }

                nodes.Add(internal_node);

                return new List<node>(new node[] { internal_node });
            }
            else
            {
                // Split the object set in half
                List<Sphere> left_objects;
                List<Sphere> right_objects;
                SplitObjectSet(objects, out left_objects, out right_objects);

                // Create a new internal node
                node internal_node = new node { self_isLeaf = 0 };

                // Set the AABB for the new node
                internal_node.aabb = bounding_box;

                // Add child nodes for the left and right object sets
                List<node> left_nodes = BuildBVHNode(left_objects, ComputeBoundingBox(left_objects), ref nodes);
                List<node> right_nodes = BuildBVHNode(right_objects, ComputeBoundingBox(right_objects), ref nodes);
                int left_child_index = nodes.Count;
                nodes.AddRange(left_nodes);
                int right_child_index = nodes.Count;
                nodes.AddRange(right_nodes);
                internal_node.SetChild(0, left_child_index);
                internal_node.SetChild(1, right_child_index);

                nodes.Add(internal_node);

                return new List<node>(new node[] { internal_node });
            }
        }

        private AABB ComputeBoundingBox(List<Sphere> objects)
        {
            if (objects.Count == 0)
            {
                return new AABB();
            }
            else
            {
                // Compute the bounding box of the first object
                AABB bounding_box = new AABB(objects[0].center - objects[0].radius, objects[0].center + objects[0].radius);

                // Expand the bounding box to include all other objects
                for (int i = 1; i < objects.Count; i++)
                {
                    AABB sphere_box = new AABB(objects[i].center - objects[i].radius, objects[i].center + objects[i].radius);
                    bounding_box = AABB.Merge(bounding_box, sphere_box);
                }

                return bounding_box;
            }
        }

        private void SplitObjectSet(List<Sphere> objects, out List<Sphere> left_objects, out List<Sphere> right_objects)
        {
            // Compute the center point of the object set
            Vector3 center = ComputeCenter(objects);

            // Determine which axis to split along
            int split_axis = DetermineSplitAxis(objects, center);

            // Sort the objects by their position on the split axis
            objects.Sort((a, b) => a.center[split_axis].CompareTo(b.center[split_axis]));

            // Split the object set in half
            int half_count = objects.Count / 2;
            left_objects = objects.GetRange(0, half_count);
            right_objects = objects.GetRange(half_count, objects.Count - half_count);
        }

        private Vec3 ComputeCenter(List<Sphere> objects)
        {
            Vec3 center = new Vec3();

            foreach (Sphere sphere in objects)
            {
                center += sphere.center;
            }

            center /= objects.Count;

            return center;
        }

        private int DetermineSplitAxis(List<Sphere> objects, Vec3 center)
        {
            // Compute the variance of the object positions along each axis
            Vec3 variance = new Vec3();

            foreach (Sphere sphere in objects)
            {
                Vec3 offset = sphere.center - center;
                variance += new Vec3(offset.x * offset.x, offset.y * offset.y, offset.z * offset.z);
            }

            // Determine which axis has the highest variance
            int split_axis = 0;

            if (variance.y > variance.x)
            {
                split_axis = 1;
            }

            if (variance.z > variance[split_axis])
            {
                split_axis = 2;
            }

            return split_axis;
        }


    }

    public struct DEVICE_BVH
    {
        // location in the array of the root BVH node
        public long root;
        // linear array of nodes, id corresponds to position in array
        public ArrayView1D<node, Stride1D.Dense> nodes;

        // linear array of spheres, contained in 
        public ArrayView1D<Sphere, Stride1D.Dense> objects;

        public DEVICE_BVH(MemoryBuffer1D<node, Stride1D.Dense> nodes, MemoryBuffer1D<Sphere, Stride1D.Dense> objects)
        {
            root = nodes.Length - 1;
            this.nodes = nodes;
            this.objects = objects;
        }

        public node GetNode(int id)
        {
            if (id >= 0 && id < nodes.Length)
            {
                return nodes[id];
            }

            Debug.Assert(false, "invalid node index");
            return new node();
        }

        public Sphere GetSphere(int id)
        {
            if (id >= 0 && id < objects.Length)
            {
                return objects[id];
            }

            Debug.Assert(false, "invalid sphere index");
            return new Sphere();
        }

        public int Trace(Ray ray, out float closest_t)
        {
            FixedStack stack = new FixedStack();
            int hit_sphere_id = -1;
            closest_t = float.MaxValue;

            stack.Push((int)root);

            while (!stack.IsEmpty)
            {
                int node_id = stack.Pop();
                node current_node = GetNode(node_id);

                // Check if the current node is intersected by the ray
                float t = current_node.intersect(ray);
                if (t > closest_t) continue;

                // If the current node is a leaf, test the contained sphere(s) for intersection
                if (current_node.isLeaf())
                {
                    for (int i = 0; i < current_node.childrenCount; i++)
                    {
                        int obj_id = current_node.GetChild(i);
                        if (obj_id != -1)
                        {
                            Sphere obj_sphere = GetSphere(obj_id);
                            float sphere_t = obj_sphere.intersect(ray);

                            if (sphere_t < closest_t)
                            {
                                closest_t = sphere_t;
                                hit_sphere_id = obj_id;
                            }
                        }
                    }
                }
                // If the current node is not a leaf, push its children onto the stack
                else
                {
                    int left_child_id = current_node.GetChildNode(0);
                    int right_child_id = current_node.GetChildNode(1);

                    if (left_child_id != -1)
                    {
                        stack.Push(left_child_id);
                    }

                    if (right_child_id != -1)
                    {
                        stack.Push(right_child_id);
                    }
                }
            }

            return hit_sphere_id;
        }

    }

    public unsafe struct FixedStack
    {
        public const int MaxStackSize = 10000;

        public fixed int stackData[MaxStackSize];
        public volatile int top;

        public bool IsEmpty => top < 0;

        public void Push(int value)
        {
            if (top < MaxStackSize - 1)
            {
                stackData[++top] = value;
            }
            else
            {
                Debug.Assert(false, "stack overflow");
            }
        }

        public int Pop()
        {
            if (top >= 0)
            {
                return stackData[top--];
            }

            Debug.Assert(false, "stack underflow");
            return -1;
        }

        public int Peek()
        {
            if (top >= 0)
            {
                return stackData[top];
            }

            Debug.Assert(false, "stack underflow");
            return -1;
        }
    }

    public unsafe struct node
    {
        public const int child_count = 4;
        public int self_isLeaf = 0; // 0 == FALSE, 1 == TRUE
        public fixed int children[child_count];
        public int childrenCount;

        public AABB aabb = default;

        public node()
        {
            for (int i = 0; i < child_count; i++)
            {
                children[i] = -1;
            }
        }

        public int GetChild(int index)
        {
            if (isLeaf())
            {
                return children[index];
            }
            else
            {
                Debug.Assert(false, "not a leaf");
                return -1;
            }
        }

        public int GetChildNode(int index)
        {
            if (!isLeaf())
            {
                return children[index];
            }
            else
            {
                Debug.Assert(false, "not not a leaf");
                return -1;
            }
        }

        public void SetChild(int index, int child_id)
        {
            if (isLeaf() && index >= 0 && index < child_count && child_id >= 0)
            {
                children[index] = child_id;
            }
            else if (!isLeaf() && index >= 0 && index < 2 && child_id >= 0)
            {
                children[index] = child_id;
            }
            else
            {
                Debug.Assert(false, "not a leaf");
            }
        }

        public bool isLeaf()
        {
            return self_isLeaf == 1;
        }

        public float intersect(Ray ray)
        {
            return aabb.Intersect(ray, out float t) ? t : float.MaxValue;
        }
    }
}
