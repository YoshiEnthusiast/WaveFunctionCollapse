using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WaveFunctionCollapseAlgorithm.Examples
{
    public static class BitmapUtilities
    {
        private const double Dpi = 96;
        private const int ColorComponentsCount = 4;

        public static Color[,] ExtractColors(BitmapSource bitmap)
        {
            var convertedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            int width = convertedBitmap.PixelWidth;
            int height = convertedBitmap.PixelHeight;    

            Color[,] result = new Color[width, height];

            int stride = GetStride(width);
            byte[] buffer = CreateBuffer(height, stride);

            convertedBitmap.CopyPixels(buffer, stride, 0);

            for (int y = 0; y < height; y++)
            {
                int row = GetRow(y, stride);

                for (int x = 0; x < width; x++)
                {
                    int position = GetPixelPosition(x, row);

                    byte b = buffer[position];
                    byte g = buffer[position + 1];
                    byte r = buffer[position + 2];
                    byte a = buffer[position + 3];

                    result[x, y] = Color.FromArgb(a, r, g, b);
                }
            }

            return result;
        }

        public static byte[] FillBuffer(int width, int height, Color with)
        {
            int stride = GetStride(width);
            byte[] buffer = CreateBuffer(height, stride);

            for (int y = 0; y < height; y++)
            {
                int row = GetRow(y, stride);

                for (int x = 0; x < width; x++)
                {
                    int position = GetPixelPosition(x, row);

                    WriteColorToBuffer(with, buffer, position);
                }
            }

            return buffer;
        }

        public static void WritePixelsToBuffer(int width, byte[] buffer, IEnumerable<Pixel> pixels)
        {
            int stride = GetStride(width);

            foreach (Pixel pixel in pixels)
            {
                Color color = pixel.Color;

                int row = GetRow(pixel.Y, stride);
                int position = GetPixelPosition(pixel.X, row);

                WriteColorToBuffer(color, buffer, position);
            }
        }

        public static Color GetAverageColor(IEnumerable<Color> colors)
        {
            int totalR = 0;
            int totalG = 0;
            int totalB = 0;
            int totalA = 0;

            foreach (Color color in colors)
            {
                totalR += color.R;
                totalG += color.G;
                totalB += color.B;
                totalA += color.A;
            }

            int count = colors.Count();

            byte r = (byte)(totalR / count);
            byte g = (byte)(totalG / count);
            byte b = (byte)(totalB / count);
            byte a = (byte)(totalA / count);

            return Color.FromArgb(a, r, g, b);
        }

        public static BitmapSource CreateBitmap(int width, int height, byte[] buffer)
        {
            return CreateBitmap(width, height, buffer, GetStride(width));
        }

        private static int GetStride(int width)
        {
            return width * ColorComponentsCount;
        }

        private static int GetPixelPosition(int x, int row)
        {
            return row + x * ColorComponentsCount;
        }

        private static int GetRow(int y, int stride)
        {
            return y * stride;
        }

        private static byte[] CreateBuffer(int height, int stride)
        {
            return new byte[height * stride];
        }

        private static BitmapSource CreateBitmap(int width, int height, byte[] buffer, int stride)
        {
            return BitmapSource.Create(width, height, Dpi, Dpi, PixelFormats.Bgra32, null, buffer, stride);
        }

        private static void WriteColorToBuffer(Color color, byte[] buffer, int position)
        {
            buffer[position] = color.B;
            buffer[position + 1] = color.G;
            buffer[position + 2] = color.R;
            buffer[position + 3] = color.A;
        }
    }
}
