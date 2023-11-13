# Tutorial 1: Understanding the SDF RenderMode in ILGPUView2

## Introduction

Lets start with volume rendering, and at its most basic, we delve into ray marching. Imagine pointing out from a pixel and stepping forward unit by unit. With each step, we check for a collision - are we hitting something? If so, the color of that something becomes the color of our pixel. Applying this method to every pixel across the screen, we end up producing an entire image.

This process is akin to ray tracing, but taken one step at a time. The twist here comes with the use of a Signed Distance Field (SDF). It allows for some unusual and fancy mixing effects. Here’s how: for every pixel, we calculate the distance to the nearest object. If this distance is short enough, there we go, we have found our collision.

With an SDF, we can draw simple shapes with smooth edges, volumes

## SDF Render Mode

SDF implements the IRenderCallback interface which means its a 

<<<csharp
    public class SDF : IRenderCallback
    { 
        SDFRenderer renderer;
        Vec3 cameraPosition = new Vec3(0, 0, -10);
        
        // this is called once, to setup the RenderMode.
        // here we just initialize the renderer struct
        public void OnStart(Renderer gpu)
        {
            renderer = new SDFRenderer();
        }

        // this is called with key events from the wpf window
        // not perfect for input, but better than nothing
        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {
            float step = 0.1f;
            if (key == Key.W) cameraPosition.z += step;
            if (key == Key.S) cameraPosition.z -= step;
            if (key == Key.A) cameraPosition.x -= step;
            if (key == Key.D) cameraPosition.x += step;
            if (key == Key.Q) cameraPosition.y += step;
            if (key == Key.E) cameraPosition.y -= step;
        }

        // this is called once per frame and is where the magic happens!
        public void OnRender(Renderer gpu)
        {
            // first we update the camera data in the renderer struct
            renderer.UpdateCameraPos(cameraPosition);

            // then we use this function to call the renderer.Apply() function 
            // on every pixel in gpu.framebuffer
            // this is where we actually draw the sdf on the GPU
            gpu.ExecuteFilter(gpu.framebuffer, renderer);
        }

        // other IRenderCallback calls ignored for length
    }
>>>

## Signed Distance Functions (SDFs)

SDFs are how we represent shapes in the SDF rendering mode. They allow us to calculate the distance between a shape and a point in space. To make the functions simpler they are implemented assuming the shape is centered on (0, 0, 0) so we need to convert out points into model space before passing them into these functions.

All the SDFs take the parameter `Vec3 p` which is the point in model space, and a parameter that specifies the shape.

You can learn many more from [an incredible resource from Inigo Quilez](https://iquilezles.org/articles/distfunctions/)

### Sphere SDF

<<<
    public static float SphereSDF(Vec3 p, float radius)
    {
        return p.length() - radius;
    }
>>>

The Sphere SDF calculates the length of the vector `Vec3 p` (the distance from the point to the center of the sphere) and subtracts the `float radius` of the sphere. 

- If result  < 0, the point is inside the sphere.
- If result  > 0, it's outside.
- If result == 0, it's exactly on the surface.


## Storing the Data

Rendering multiple primitives on the GPU requires an efficient way to pass data between the GPU and the CPU. In our case, we use the `SDFRenderer` struct for this purpose. However, there's a significant limitation: the `IImageFilter` interface, which `SDFRenderer` implements, requires that any implementor must be unmanaged. This means the struct cannot contain references, and the only viable option to store substantial data is through fixed-size arrays.

<<<
    public unsafe struct SDFRenderer : IImageFilter
    {
        // Define a fixed number of primitives
        const int numPrimitives = 25;

        // Fixed arrays to store primitive data
        public fixed int types[numPrimitives];
        public fixed float modelMatricies[numPrimitives * 16];
        public fixed float param1[numPrimitives];
        public fixed float param2[numPrimitives];
        public fixed float param3[numPrimitives];
        public fixed int colors[numPrimitives];

        // Normal unmanaged types also work
        public Vec3 cameraPos;
        ...
    }
>>>

The `SDFRenderer` struct is packed with fixed arrays to store various properties of each primitive:

- `types[]`: Holds the type of each primitive (e.g., sphere, box, cylinder).
- `modelMatricies[]`: Contains the model matrices, which include translation, rotation, and scale for each primitive. These matrices are crucial for positioning and orienting the primitives in 3D space.
- `param1[]`, `param2[]`, `param3[]`: These arrays store specific parameters for different types of primitives, like radius for spheres or dimensions for boxes.
- `colors[]`: Each primitive's color is stored here, adding a visual variety to the rendered scene.

By utilizing these fixed arrays, `SDFRenderer` effectively bridges the GPU and CPU, allowing for dynamic and varied rendering of multiple primitives in the scene. This method is a direct response to the limitations imposed by the `IImageFilter` interface, showcasing a creative solution to overcome the challenge of unmanaged data requirements in GPU rendering.

## Rendering the Scene

The heart of the rendering process in our SDF RenderMode is encapsulated in the `Apply` method of the `SDFRenderer` struct. This function is special in that it is run on the GPU by the line:

<<<
    gpu.ExecuteFilter(gpu.framebuffer, renderer);
>>>

by the SDF RenderModes OnRender() function. 

Apply is called once for each pixel in the output and the // TODO pickup here

<<<
    public RGBA32 Apply(int tick, float x, float y, dImage output)
    {
        // Establishing the ray properties
        float aspectRatio = (float)output.width / (float)output.height;
        Vec3 rayOrigin = cameraPos;
        Vec3 rayDir = new Vec3((x - 0.5f) * aspectRatio, y - 0.5f, 1).Normalize();

        // Ray marching loop
        float t = 0;
        float maxDistance = 100.0f; // Defines the furthest distance we'll check for collisions

        for (int i = 0; i < 128; ++i)
        {
            // Determine the current point along the ray
            Vec3 point = rayOrigin + rayDir * t;

            float closestDistance = float.MaxValue;
            int closestPrimitive = -1;

            // Checking each primitive for the closest one
            for (int j = 0; j < numPrimitives; ++j)
            {
                fixed (float* matrixPtr = &modelMatricies[j * 16])
                {
                    Mat4x4* modelMatrix = (Mat4x4*)matrixPtr;
                    Vec4 transformedPoint4D = modelMatrix->MultiplyVector(point);

                    // Determine the distance to this primitive
                    float d = DetermineDistanceToPrimitive(types[j], transformedPoint4D, param1[j], param2[j], param3[j]);

                    // Update the closest distance and primitive
                    if (d < closestDistance)
                    {
                        closestDistance = d;
                        closestPrimitive = j;
                    }
                }
            }

            // Check if we've hit the surface of the closest primitive
            if (closestDistance < 0.001f && closestPrimitive != -1)
            {
                return new RGBA32(colors[closestPrimitive]); // Color the pixel based on the primitive's color
            }

            // Advance the ray forward
            t += closestDistance * 0.9f;

            // Break the loop if the ray has travelled beyond the max distance without a hit
            if (t > maxDistance) break;
        }

        return new RGBA32(0, 0, 0); // Return black if no hit is found
    }
>>>

In this method:

1. **Ray Setup**: We define the ray's origin and direction for each pixel, considering the aspect ratio of the output image.
2. **Ray Marching**: The for-loop represents the ray marching process, where we step through the scene along the ray.
3. **Primitive Collision Check**: Within each step, we check each primitive in the scene to find the one closest to the ray.
4. **Surface Hit Detection**: When the ray is sufficiently close to a surface (i.e., the distance is below a small threshold), we determine that a collision has occurred.
5. **Color Assignment**: Upon a collision, the pixel is colored based on the closest hit primitive's color.
6. **Loop Exit Conditions**: The loop continues until the ray either hits a surface or reaches a maximum distance without any collision.

This `Apply` method encapsulates the essence of SDF-based rendering, employing ray marching to determine pixel colors and thus render the entire scene.

## Conclusion