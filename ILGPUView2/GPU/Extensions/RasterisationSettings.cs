using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILGPUView2.GPU.Extensions
{
    public class RasterisationSettings
    {
        public const bool debugCountColor = false;

        // this controls how big the tiles are
        //public const int tileSize = 8; // 7.8 ms
        public const int tileSize = 4; // 5.5 ms
        //public const int tileSize = 2; // 256 ms

        // controls the max triangles per tile to be drawn 
        // effectively we always need "enough"
        // smaller tiles help make this less important
        public const int maxTrianglesPerTile = 1024;

        //public const int transformGroupSize = 1024; // 3 ms
        //public const int transformGroupSize = 512; // 2.1 ms
        //public const int transformGroupSize = 256; // 1.9 ms
        //public const int transformGroupSize = 128; // 1.7 ms
        public const int transformGroupSize = 64; // 1.6 ms
        //public const int transformGroupSize = 32; // 2.3 ms

        //public const int tileFillGroupSize = 1024; //8.3 ms
        //public const int tileFillGroupSize = 512; //6.6 ms
        //public const int tileFillGroupSize = 256; //6.3 ms
        public const int tileFillGroupSize = 128; //6.2 ms
        //public const int tileFillGroupSize = 64; //6.2 ms
        //public const int tileFillGroupSize = 32; //6.2 ms

        //public const int drawFillGroupSize = 1024; // 13.1 ms
        //public const int drawFillGroupSize = 512;  // 13.1 ms
        public const int drawFillGroupSize = 256;  // 13 ms
        //public const int drawFillGroupSize = 128;  // 13 ms
        //public const int drawFillGroupSize = 64;   // 13 ms
        //public const int drawFillGroupSize = 32;   // 13 ms
    }
}
