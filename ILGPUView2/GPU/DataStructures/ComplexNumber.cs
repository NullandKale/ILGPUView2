using System;

namespace GPU
{
    public struct ComplexNumber
    {
        public double Real;
        public double Imaginary;

        public ComplexNumber(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public static ComplexNumber operator +(ComplexNumber a, ComplexNumber b)
        {
            return new ComplexNumber(a.Real + b.Real, a.Imaginary + b.Imaginary);
        }

        public static ComplexNumber operator *(ComplexNumber a, ComplexNumber b)
        {
            double real = a.Real * b.Real - a.Imaginary * b.Imaginary;
            double imaginary = a.Real * b.Imaginary + a.Imaginary * b.Real;
            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber FromPolarCoordinates(double magnitude, double phase)
        {
            return new ComplexNumber(magnitude * Math.Cos(phase), magnitude * Math.Sin(phase));
        }

        // Complex-Float multiplication
        public static ComplexNumber operator *(ComplexNumber a, double b)
        {
            return new ComplexNumber(a.Real * b, a.Imaginary * b);
        }

        // Float-Complex multiplication
        public static ComplexNumber operator *(double a, ComplexNumber b)
        {
            return new ComplexNumber(a * b.Real, a * b.Imaginary);
        }


        public double Magnitude => Math.Sqrt(Real * Real + Imaginary * Imaginary);
        public double Phase => Math.Atan2(Imaginary, Real);
    }
}
