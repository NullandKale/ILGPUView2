using GPU;
using System;
using ILGPU.Algorithms;
using System.Threading;
using System.Threading.Tasks;
using ILGPU.Algorithms.Random;

namespace ExampleProject.Modes
{
    public unsafe struct BouncingCirclesFilter : IImageFilter
    {
        const int spheres = 50;
        
        float largeCircleRadius;
        Vec2 largeCircleCenter;

        float smallCircleRadius;

        fixed float xPositions[spheres];
        fixed float yPositions[spheres];

        fixed float xVelocities[spheres];
        fixed float yVelocities[spheres];
        
        fixed float colorH[spheres];

        public int tick;
        public float damping = 1f;
        public float velocityMagnitude = 100f;
        public float gravityMagnitude = 0f;
        public float gravityDirection = 0f;

        public BouncingCirclesFilter(int tick, float frameWidth, float frameHeight)
        {
            this.tick = tick;

            // Calculate the largest circle's properties
            largeCircleRadius = Math.Min(frameWidth, frameHeight) / 2.0f;
            largeCircleCenter = new Vec2(frameWidth / 2.0f, frameHeight / 2.0f);
            smallCircleRadius = largeCircleRadius / 30.0f;

            Reset();
        }

        public void Reset()
        {
            Random rng = new Random();

            for (int i = 0; i < spheres; i++)
            {
                // Initialize positions for smaller circles within the large circle
                float angle = (float)(rng.NextDouble() * 2 * Math.PI);

                // Adjust the distribution for the distance to account for circular area
                float radiusRatio = (float)Math.Sqrt(rng.NextDouble());
                float distance = radiusRatio * (largeCircleRadius - smallCircleRadius);

                xPositions[i] = largeCircleCenter.x + distance * (float)Math.Cos(angle);
                yPositions[i] = largeCircleCenter.y + distance * (float)Math.Sin(angle);

                // Initialize random velocities for smaller circles
                angle = (float)(rng.NextDouble() * 2 * Math.PI);
                xVelocities[i] = velocityMagnitude * (float)Math.Cos(angle);
                yVelocities[i] = velocityMagnitude * (float)Math.Sin(angle);

                // Random color hue for each circle
                colorH[i] = (float)rng.NextDouble();
            }
        }

        private void ApplyGravity(int index, float deltaTime)
        {
            Vec2 gravityVector = new Vec2(XMath.Cos(gravityDirection), XMath.Sin(gravityDirection)) * gravityMagnitude;
            xVelocities[index] += gravityVector.x * deltaTime;
            yVelocities[index] += gravityVector.y * deltaTime;
        }

        private void UpdatePosition(int index, float deltaTime)
        {
            xPositions[index] += xVelocities[index] * deltaTime;
            yPositions[index] += yVelocities[index] * deltaTime;
        }

        private void HandleBoundaryCollision(int index, float deltaTime)
        {
            Vec2 circleCenter = new Vec2(xPositions[index], yPositions[index]);
            float distanceFromLargeCircleCenter = Vec2.Distance(circleCenter, largeCircleCenter);

            if (distanceFromLargeCircleCenter + smallCircleRadius > largeCircleRadius)
            {
                // Correct position to be exactly on the boundary
                float correctionRatio = (largeCircleRadius - smallCircleRadius) / distanceFromLargeCircleCenter;
                xPositions[index] = largeCircleCenter.x + (circleCenter.x - largeCircleCenter.x) * correctionRatio;
                yPositions[index] = largeCircleCenter.y + (circleCenter.y - largeCircleCenter.y) * correctionRatio;

                // Reflect the velocity vector
                Vec2 directionToCenter = Vec2.Normalize(largeCircleCenter - circleCenter);
                Vec2 velocity = new Vec2(xVelocities[index], yVelocities[index]);
                Vec2 reflectedVelocity = velocity - 2 * Vec2.Dot(velocity, directionToCenter) * directionToCenter;
                xVelocities[index] = reflectedVelocity.x * damping;
                yVelocities[index] = reflectedVelocity.y * damping;
            }
        }

        private void HandleParticleCollision(int index1, int index2, float deltaTime)
        {
            Vec2 posI = new Vec2(xPositions[index1], yPositions[index1]);
            Vec2 posJ = new Vec2(xPositions[index2], yPositions[index2]);
            Vec2 velI = new Vec2(xVelocities[index1], yVelocities[index1]);
            Vec2 velJ = new Vec2(xVelocities[index2], yVelocities[index2]);

            float distance = Vec2.Distance(posI, posJ);
            if (distance < 2 * smallCircleRadius)
            {
                // Correct overlap
                float overlap = (2 * smallCircleRadius - distance) / 2;
                Vec2 direction = Vec2.Normalize(posJ - posI);
                posI -= direction * overlap;
                posJ += direction * overlap;

                // Adjust velocities for elastic collision
                Vec2 newVelI = velI - 2 * Vec2.Dot(velI - velJ, direction) / 2 * direction;
                Vec2 newVelJ = velJ - 2 * Vec2.Dot(velJ - velI, direction) / 2 * direction;

                xPositions[index1] = posI.x;
                yPositions[index1] = posI.y;
                xVelocities[index1] = newVelI.x * damping;
                yVelocities[index1] = newVelI.y * damping;

                xPositions[index2] = posJ.x;
                yPositions[index2] = posJ.y;
                xVelocities[index2] = newVelJ.x * damping;
                yVelocities[index2] = newVelJ.y * damping;
            }
        }


        public void Update(float frametimeMS)
        {
            float deltaTime = frametimeMS / 1000.0f; // Convert milliseconds to seconds

            BouncingCirclesFilter localCopy = this;

            for (int i = 0; i < spheres; i++)
            {
                ApplyGravity(i, deltaTime);
                UpdatePosition(i, deltaTime);
                HandleBoundaryCollision(i, deltaTime);

                for (int j = i + 1; j < spheres; j++)
                {
                    HandleParticleCollision(i, j, deltaTime);
                }
            }

        }

        // x, y are from [0 - 1]
        // this is effectively a frag shader run on the gpu
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            // Draw the large circle as a white border
            Vec2 point = new Vec2(x * framebuffer.width, y * framebuffer.height);
            if (Math.Abs(Vec2.Distance(point, largeCircleCenter) - largeCircleRadius) < 1f)
            {
                return new RGBA32(new Vec3(1.0f, 1.0f, 1.0f)); // white color
            }

            // Check if point is inside any of the smaller circles
            for (int i = 0; i < spheres; i++)
            {
                Vec2 center = new Vec2(xPositions[i], yPositions[i]);
                if (Vec2.Distance(point, center) <= smallCircleRadius)
                {
                    // Color the pixel based on the circle's color
                    Vec3 color = Vec3.HsbToRgb(new Vec3((float)colorH[i], 1, 1));

                    return new RGBA32(color);
                }
            }

            // Default background color
            return new RGBA32(new Vec3(0, 0, 0));
        }

    }
}
