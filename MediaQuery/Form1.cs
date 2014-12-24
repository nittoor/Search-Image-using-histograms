﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace MediaQuery
{
    public partial class Form1 : Form
    {
        private string m_queryFile;                 // input RGB logo to be searched
         private string m_queryAlphaFile = "";       // alpha levels corresponding to the query file (optional)
        private string m_searchFile;                // input RGB file to be searched for the given logo
        private bool m_showCorners;                 // if we paint the corners on block hits
        private Histogram m_h1;
        private Histogram m_h2;
        private Histogram m_h3;
        private Histogram m_h4;
        private int m_binSize1, m_binSize2, m_binSize3;
        private Clumps clumpRecognition  = new Clumps();
        private Clumps clumpRecognitionSat = new Clumps();

        public Form1(string[] args)
        {
            InitializeComponent();

            // GUI labels
            label_studentIDs.Text = "Ryan Logsdon (ID 9800-2889-18), Prashant Nittoor (ID 1339-1322-49), IChun Chen (ID 2700-5160-07)";
            label_queryImage.Text = "Query Image";
            label_searchImage.Text = "Search Image";

            // take in the parameters
            if (args.Length == 0)               // for testing purposes, use the hard-coded values when there are no args supplied 
            {
                m_queryFile = @"C:\576\images\starbucks.rgb";
                //m_queryAlphaFile = @"C:\576\images\orange.alpha";
                m_searchFile = @"C:\576\images\starbucks2.rgb";
                m_showCorners = true;
            }
            else                                // else the args have been properly supplied
            {
                m_queryFile = args[0];
                m_queryAlphaFile = args[1];     // if it's empty, input empty quotes ""
                m_searchFile = args[2];
                if (args.Length > 3)
                    m_showCorners = args[3] == "1" ? true : false;
            }
        }

        private void onFormShown(object sender, EventArgs e)
        {
            
            // load the query image and display
            Image queryImage = new Image(m_queryFile);
            Image queryImageSat = new Image(m_queryFile, m_queryAlphaFile);
            queryImageSat.GenerateSaturatedHSV();
            pictureBox_queryImage.Image = queryImage.BitmapImage;
            pictureBox_queryImage.Update();

            // load the search image and display
            Image searchImage_Math = new Image(m_searchFile);
            Image searchImage_Sat = new Image(m_searchFile);
            Image searchImage_GUI = new Image(m_searchFile);

            searchImage_Sat.GenerateSaturatedHSV();

            pictureBox_searchImage.Image = searchImage_Math.BitmapImage;
            pictureBox_searchImage.Update();
            
            // always apply the SuperRed filter
            searchImage_Math.SuperRed();

            // set up the histogram bin sizes and the full-image histograms
            m_binSize1 = 4;
            m_binSize2 = 4;
            m_binSize3 = 4;
            m_h1 = CreateHistogram(queryImage, m_binSize1, m_binSize2, m_binSize3);
            m_h2 = CreateHistogram(searchImage_Math, m_binSize1, m_binSize2, m_binSize3);
            m_h3 = CreateHistogram(searchImage_Sat, m_binSize1, m_binSize2, m_binSize3);
            m_h4 = CreateHistogram(queryImageSat, m_binSize1, m_binSize2, m_binSize3);

            //pictureBox_histogram1.Update();     // force a call to repaint the histogram
            
            // 8x8 histograms to find the edges of the object in question
            BlockHisto blocks = new BlockHisto(searchImage_Math.BitmapImage, m_binSize1, m_binSize2, m_binSize3);
            for (int y=0;y<blocks.NumBlocksDown; y++)
                for (int x = 0; x < blocks.NumBlocksAcross; x++)
                {
                    Histogram h = blocks.GetHistogramAtBlock(x, y);
                    float bhatt = Bhattacharyya(m_h1, h);

                    if (bhatt > 0.5)
                    {
                            if (m_showCorners)
                                searchImage_GUI.PrintCorner(x * 8, y * 8, 8, 8);
                            clumpRecognition.checkAdjacencyAndAdd(x * 8, y * 8);
                    }
                }
            Console.WriteLine("no of objects before" + clumpRecognition.numberOfShapes());
            clumpRecognition.computeAllShapes();
            Console.WriteLine("no of objects after merging " + clumpRecognition.numberOfShapes());
            //clumpRecognition.keepTopX(5);
            Console.WriteLine("no of objects after top 5 " + clumpRecognition.numberOfShapes());
            List<float> avgHistogramArray = new List<float>();
            foreach (List<Point> shape in clumpRecognition.ListOfShapes)
            {
                float sum = 0;
                int count = 0;
                foreach (Point rect in shape)
                {
                    if (searchImage_Math.GetAvgBrightnessForBlock(rect.X, rect.Y, 8, 8) > 0.2)
                    {
                        Histogram h = blocks.GetHistogramAtBlock(rect.X / 8, rect.Y / 8);
                        float bhatt = Bhattacharyya(m_h1, h);
                        sum += bhatt;
                    }
                        count++;
                }
                avgHistogramArray.Add(sum / count);
                //Console.WriteLine("average hist for object =" + (sum / count) + "at" + shape[0]);
            }

            float max = -1; int pos = -1;
            for (int i = 0; i < avgHistogramArray.Count; i++)
            {
                Console.WriteLine("working on shape " + clumpRecognition.ListOfShapes[i][0]);
                if (max < avgHistogramArray[i])
                {
                    max = avgHistogramArray[i];
                    pos = i;
                }
            }
            Console.WriteLine("postion of best object= " + pos + "no of objects = " + avgHistogramArray.Count);



            /**************************************************************************************/
            // 8x8 histograms to find the edges of the object in question
            BlockHisto blocksSat = new BlockHisto(searchImage_Sat.BitmapImage, m_binSize1, m_binSize2, m_binSize3);
            for (int y = 0; y < blocksSat.NumBlocksDown; y++)
                for (int x = 0; x < blocksSat.NumBlocksAcross; x++)
                {
                    Histogram h = blocksSat.GetHistogramAtBlock(x, y);
                    float bhatt = Bhattacharyya(m_h4, h);

                    if (bhatt > 0.5)
                    {
                        if (m_showCorners)
                            searchImage_GUI.PrintCorner(x * 8, y * 8, 8, 8);
                        clumpRecognitionSat.checkAdjacencyAndAdd(x * 8, y * 8);
                    }
                }
            Console.WriteLine("no of objects before" + clumpRecognitionSat.numberOfShapes());
            clumpRecognitionSat.computeAllShapes();
            Console.WriteLine("no of objects after merging " + clumpRecognitionSat.numberOfShapes());
            //clumpRecognition.keepTopX(5);
            Console.WriteLine("no of objects after top 5 " + clumpRecognitionSat.numberOfShapes());
            List<float> avgHistogramArraySat = new List<float>();
            foreach (List<Point> shape in clumpRecognitionSat.ListOfShapes)
            {
                float sum = 0;
                int count = 0;
                foreach (Point rect in shape)
                {
                    if (searchImage_Sat.GetAvgBrightnessForBlock(rect.X, rect.Y, 8, 8) > 0.2)
                    {
                        Histogram h = blocks.GetHistogramAtBlock(rect.X / 8, rect.Y / 8);
                        float bhatt = Bhattacharyya(m_h4, h);
                        sum += bhatt;
                    }
                    count++;
                }
                avgHistogramArraySat.Add(sum / count);
                //Console.WriteLine("average hist for object =" + (sum / count) + "at" + shape[0]);
            }

            float maxSat = -1; int posSat = -1;
            for (int i = 0; i < avgHistogramArraySat.Count; i++)
            {
                Console.WriteLine("working on shape " + clumpRecognitionSat.ListOfShapes[i][0]);
                if (maxSat < avgHistogramArraySat[i])
                {
                    maxSat = avgHistogramArraySat[i];
                    posSat = i;
                }
            }
            Console.WriteLine("postion of best object sat= " + posSat + "no of objects = " + avgHistogramArraySat.Count);

            
            /***********************************************************************************************/
            bool isMatch=false;


            if ((clumpRecognitionSat.ListOfShapes.Count == 0 && clumpRecognition.ListOfShapes.Count > 0) ||
                (clumpRecognition.ListOfShapes.Count == 0 && clumpRecognitionSat.ListOfShapes.Count > 0) ||
                (clumpRecognition.ListOfShapes.Count == 0 && clumpRecognitionSat.ListOfShapes.Count == 0))
            {
                isMatch = false;
            }
            else
            {
                List<Point> satImageBoundary = new List<Point>();
                if (clumpRecognitionSat.ListOfShapes.Count > 0 && posSat >= 0)
                {
                    satImageBoundary.Add(clumpRecognitionSat.BoundingRectForShape[posSat][0]);
                    satImageBoundary.Add(clumpRecognitionSat.BoundingRectForShape[posSat][1]);
                }

                if (satImageBoundary.Count > 0)
                {
                    Point SatVertex1 = new Point(satImageBoundary[0].X, satImageBoundary[0].Y);
                    Point SatVertex2 = new Point(satImageBoundary[0].X, satImageBoundary[1].Y);
                    Point SatVertex3 = new Point(satImageBoundary[1].X, satImageBoundary[0].Y);
                    Point SatVertex4 = new Point(satImageBoundary[1].X, satImageBoundary[1].Y);

                    List<Point> mathImageBoundary = new List<Point>();
                    if (clumpRecognition.ListOfShapes.Count > 0 && pos >= 0)
                    {
                        mathImageBoundary.Add(clumpRecognition.BoundingRectForShape[pos][0]);
                        mathImageBoundary.Add(clumpRecognition.BoundingRectForShape[pos][1]);
                    }

                    if (mathImageBoundary.Count > 0)
                    {
                        Point MathVertex1 = new Point(mathImageBoundary[0].X, mathImageBoundary[0].Y);
                        Point MathVertex2 = new Point(mathImageBoundary[0].X, mathImageBoundary[1].Y);
                        Point MathVertex3 = new Point(mathImageBoundary[1].X, mathImageBoundary[0].Y);
                        Point MathVertex4 = new Point(mathImageBoundary[1].X, mathImageBoundary[1].Y);

                        if (
                            (MathVertex1.X >= SatVertex1.X && MathVertex1.X <= SatVertex4.X && MathVertex1.Y >= SatVertex1.Y && MathVertex1.Y <= SatVertex4.Y) ||
                            (MathVertex2.X >= SatVertex1.X && MathVertex2.X <= SatVertex4.X && MathVertex2.Y >= SatVertex1.Y && MathVertex2.Y <= SatVertex4.Y) ||
                            (MathVertex3.X >= SatVertex1.X && MathVertex3.X <= SatVertex4.X && MathVertex3.Y >= SatVertex1.Y && MathVertex3.Y <= SatVertex4.Y) ||
                            (MathVertex4.X >= SatVertex1.X && MathVertex4.X <= SatVertex4.X && MathVertex4.Y >= SatVertex1.Y && MathVertex4.Y <= SatVertex4.Y) ||

                            (SatVertex1.X >= MathVertex1.X && SatVertex1.X <= MathVertex4.X && SatVertex1.Y >= MathVertex1.Y && SatVertex1.Y <= MathVertex4.Y) ||
                            (SatVertex2.X >= MathVertex1.X && SatVertex2.X <= MathVertex4.X && SatVertex2.Y >= MathVertex1.Y && SatVertex2.Y <= MathVertex4.Y) ||
                            (SatVertex3.X >= MathVertex1.X && SatVertex3.X <= MathVertex4.X && SatVertex3.Y >= MathVertex1.Y && SatVertex3.Y <= MathVertex4.Y) ||
                            (SatVertex4.X >= MathVertex1.X && SatVertex4.X <= MathVertex4.X && SatVertex4.Y >= MathVertex1.Y && SatVertex4.Y <= MathVertex4.Y)
                            )
                        {
                            isMatch = true;
                        }
                    }
                }
            }

            textBox1.Text = isMatch.ToString();
            textBox1.Update();

            clumpRecognition.drawBestBoundaries(pos, searchImage_GUI);
            clumpRecognitionSat.drawBestBoundaries(posSat, searchImage_Sat);
            //clumpRecognition.drawBoundaries(searchImage_GUI);
            pictureBox_searchImage.Image = searchImage_GUI.BitmapImage;
            pictureBox_queryImage.Image = searchImage_Sat.BitmapImage;
            pictureBox_queryImage.Update();
            pictureBox_searchImage.Update();
             
        }

        private unsafe Histogram CreateHistogram(Image img, int numBinsCh1, int numBinsCh2, int numBinsCh3)
        {
            Bitmap bmp = img.BitmapImage;
            Histogram hist = new Histogram(numBinsCh1 * numBinsCh2 * numBinsCh3);
            float total = 0;
            int idx = 0;

            UnsafeBitmap fastBitmap = new UnsafeBitmap(bmp);
            fastBitmap.LockBitmap();
            Point size = fastBitmap.Size;
            BGRA* pPixel;

            for (int y = 0; y < size.Y; y++)
            {
                pPixel = fastBitmap[0, y];
                for (int x = 0; x < size.X; x++)
                {
                    if (img.HasAlphaMask(x, y))
                        continue;

                    //get the bin index for the current pixel colour
                    idx = GetSingleBinIndex(numBinsCh1, numBinsCh2, numBinsCh3, pPixel);

                    if (idx < hist.Data.Length - 1 && idx != 0)
                    {
                        hist.Data[idx]++;
                        total++;
                    }
                    //increment the pointer
                    pPixel++;
                }

                if (y > size.Y / 2)
                {
                    int dummy = 1;
                }
            }

            fastBitmap.UnlockBitmap();

            //normalise
            if (total > 0)
                hist.Normalise(total);

            return hist;
        }

        private unsafe int GetSingleBinIndex(int binCount1, int binCount2, int binCount3, BGRA* pixel)
        {
            int idx = 0;

            //find the index                
            int i1 = GetBinIndex(binCount1, (float)pixel->red, 255);
            int i2 = GetBinIndex(binCount2, (float)pixel->green, 255);
            int i3 = GetBinIndex(binCount3, (float)pixel->blue, 255);
            idx = i1 + i2 * binCount1 + i3 * binCount1 * binCount2;

            return idx;
        }

        private int GetBinIndex(int binCount, float colourValue, float maxValue)
        {
            int idx = (int)(colourValue * (float)binCount / maxValue);
            if (idx >= binCount)
                idx = binCount - 1;

            return idx;
        }

        private float Bhattacharyya(Histogram hist1, Histogram hist2)
        {
            float total = 0;
            for (int i = 0; i < hist1.Data.Length; i++)
            {
                double coeff = Math.Sqrt((double)hist1.Data[i] * (double)hist2.Data[i]);
                total += (float)coeff;
            }
            return total;
        }

        private float TargetRepresentation(Histogram hist1, Histogram hist2)
        {
            float totalBinsUsed = 0;      // total bins used in the target's histogram
            float totalBinsInCommon = 0;  // total bins used in the target that are also used in the search image

            for (int i = 0; i < hist1.Data.Length; i++)
            {
                if (hist1.Data[i] == 0)
                    continue;

                totalBinsUsed++;
                if (hist2.Data[i] != 0)
                    totalBinsInCommon++;
            }
            return totalBinsInCommon / totalBinsUsed;
        }

        private void pictureBox_histogram1_paint(object sender, PaintEventArgs e)
        {
            DisplayModel(m_h1, pictureBox_histogram1.Height, pictureBox_histogram1.Width, e, Color.Red, m_binSize1 * m_binSize2 * m_binSize3);
        }

        private void pictureBox_histogram2_paint(object sender, PaintEventArgs e)
        {
            DisplayModel(m_h2, pictureBox_histogram2.Height, pictureBox_histogram2.Width, e, Color.Red, m_binSize1 * m_binSize2 * m_binSize3);
        }

        private void DisplayModel(Histogram model, int height, int width, PaintEventArgs e, Color color, int binRange)
        {
            if (model != null)
            {
                Pen p = new Pen(color, 1);
                SolidBrush b = new SolidBrush(color);
                float[] copy = new float[model.Data.Length];
                Array.Copy(model.Data, copy, model.Data.Length);
                Array.Sort(copy);
                float max = copy[copy.Length - 1];
                float scale = height / max;
                if (float.IsNaN(scale))
                    scale = 1;
                //Approximation: divide by total bins and remove 4 from width to account for picbox border
                float w = (float)(width - 4) / (float)(model.Data.Length);
                for (int count = 0; count < model.Data.Length; count++)
                {
                    if (model.Data[count] > 0)
                    {
                        e.Graphics.DrawRectangle(p, (float)count * w, (float)height - (model.Data[count] * scale), w, model.Data[count] * scale);
                        e.Graphics.FillRectangle(b, (float)count * w, (float)height - (model.Data[count] * scale), w, model.Data[count] * scale);

                    }
                }
            }
        }
    }
}
