using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using SharpDX;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;

namespace ScreenShareClient
{
    public partial class Form1 : Form
    {

        private TcpClient client;
        private Socket sck;
        private Bitmap initial,block;
        private byte[] buffer;
        private Thread thread;
        private MemoryStream ms;
        private double mbs = 0;
        private int x, y;
        private Rectangle BlockRectangle=new Rectangle(0,0,0,0);
        public Form1()
        {
            buffer = new byte[1920 * 1080 * 3];
            ms = new MemoryStream(buffer, 4, buffer.Length - 4);
            InitializeComponent();
        }
        private void PrintData()
        {

            Console.WriteLine("DataLength :" + length);
            Console.WriteLine("Block X : {0}  Y : {1}", x, y);
            Console.WriteLine("--------");
        }

        private Bitmap bufferToJpeg()
        {


            return (Bitmap)Image.FromStream(ms);
        }

        private int BlockX()
        {
            return buffer[0] | buffer[1] << 8;

        }

        private int BlockY()
        {
            return buffer[2] | buffer[3] << 8;

        }
        int length;

        private int ReadData()
        {
            int total = 0;
            int recv;
            byte[] datasize = new byte[4];

            recv = sck.Receive(datasize, 0, 4, 0);
            int size = BitConverter.ToInt32(datasize, 0);
            int dataleft = size;

            while (total < size)
            {
                recv = sck.Receive(buffer, total, dataleft, 0);
                if (recv == 0)
                {

                    break;
                }
                total += recv;
                dataleft -= recv;
            }
           //// if(size>65000)
           // Console.WriteLine(size / 1000 + "KB");
            return size;
        }
        private int ReadData2()
        {

            int sum = 0;
            byte[] header = new byte[4];
            sck.Receive(header);
            length = BitConverter.ToInt32(header, 0);
            sum = sck.Receive(buffer, length, SocketFlags.None);
            while (sum < length)
            {
                sum += sck.Receive(buffer, length, SocketFlags.None);
                Console.WriteLine("reading....");

            }
           
            return length;


        }
        private unsafe void Draw2(Bitmap bmp2, Point point)
        {
            lock (initial)
            {
                BitmapData bmData = initial.LockBits(new Rectangle(0, 0, initial.Width, initial.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, initial.PixelFormat);
                BitmapData bmData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp2.PixelFormat);
                IntPtr scan0 = bmData.Scan0;
                IntPtr scan02 = bmData2.Scan0;
                int stride = bmData.Stride;
                int stride2 = bmData2.Stride;
                int Width = bmp2.Width;
                int Height = bmp2.Height;
                int X = point.X;
                int Y = point.Y;

                scan0 = IntPtr.Add(scan0, stride * Y + X * 3);//setting the pointer to the requested line
                for (int y = 0; y < Height; y++)
                {
                    memcpy(scan0, scan02, (UIntPtr)(Width * 3));//copy one line

                    scan02 = IntPtr.Add(scan02, stride2);//advance pointers
                    scan0 = IntPtr.Add(scan0, stride);//advance pointers//
                }


                initial.UnlockBits(bmData);
                bmp2.UnlockBits(bmData2);
            }
        }
        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);
        private unsafe void Draw(Bitmap bmp2, Point point)
        {
            lock(initial)
            { 
          
                    Rectangle LockRectangle = new Rectangle(point.X, point.Y, bmp2.Width, bmp2.Height);
                   // Console.WriteLine(initial.Size.ToString());
                    
                     //Console.WriteLine(LockRectangle.ToString());
                         BitmapData bmData = initial.LockBits(LockRectangle, System.Drawing.Imaging.ImageLockMode.WriteOnly, initial.PixelFormat);
                    BitmapData bmData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp2.PixelFormat);
                    IntPtr scan0 = bmData.Scan0;
                    IntPtr scan02 = bmData2.Scan0;
                    int stride = bmData.Stride;
                    int stride2 = bmData2.Stride;
                    int Width = bmp2.Width;
                    int Height = bmp2.Height;
                    int X = point.X;
                    int Y = point.Y;

                    // scan0 = IntPtr.Add(scan0, stride * Y + X * 3);//setting the pointer to the requested line
                    for (int y = 0; y < Height; y++)
                    {
                        Utilities.CopyMemory(scan0, scan02, (Width * 3));//copy one line

                        scan02 = IntPtr.Add(scan02, stride2);//advance pointers
                        scan0 = IntPtr.Add(scan0, stride);//advance pointers//
                    }

                    /* #region checkInvalideUpdate
                      for (int y=0;y<initial.Height;y++)
                      {
                          scan0 = bmData.Scan0;
                          byte* ptr = (byte*)scan0;
                          ptr += y * stride;
                          for (int x = 0; x < initial.Width; x++)
                          {
                              ptr[0] = 255;
                              ptr[1] = 0;
                              ptr[2] = 0;
                              ptr += 3;
                          }
                      }
                      #endregion*/
                    initial.UnlockBits(bmData);
                    bmp2.UnlockBits(bmData2);

              
            }
        }

        private Rectangle GetViewRect() { return pictureBox1.ClientRectangle; }

        private void MainScreenThread()
        {

            pictureBox1.Resize += (_s, _e) =>
            {
                viewRect = GetViewRect();
                scaleX = (float)viewRect.Width / initial.Width;
                scaleY = (float)viewRect.Height / initial.Height;
                scaleXRev = (float)initial.Width / viewRect.Width;
                scaleYRev = (float)initial.Height / viewRect.Height;
                pictureBox1.Invalidate();
            };

            // The update action
            Action updateAction = () =>
            {

                var targetRect = Rectangle.FromLTRB(
                    (int)Math.Truncate(BlockRectangle.X * scaleX),
                    (int)Math.Truncate(BlockRectangle.Y * scaleY),
                    (int)Math.Ceiling(BlockRectangle.Right * scaleX),
                    (int)Math.Ceiling(BlockRectangle.Bottom * scaleY));
                // Console.WriteLine(BlockRectangle);
                pictureBox1.Invalidate(targetRect);
                
            };

            ReadData();//reading data from socket.
            client.Close();
            initial = bufferToJpeg();//first intial full screen image.
           
            pictureBox1.Paint += pictureBox1_Paint;
            BlockRectangle.X = 0;
            BlockRectangle.Y = 0;
            BlockRectangle.Width = initial.Width;
            BlockRectangle.Height = initial.Height;
            viewRect = GetViewRect();
            scaleX = (float)viewRect.Width / initial.Width;
            scaleY = (float)viewRect.Height / initial.Height;
            scaleXRev = (float)initial.Width / viewRect.Width;
            scaleYRev = (float)initial.Height / viewRect.Height;
            this.Invoke(updateAction);
            while (true)
            {

                var receivedData = udpclient.Receive(ref ep);
                //Console.WriteLine(receivedData.Length);

                x = receivedData[0] | receivedData[1] << 8;
                y = receivedData[2] | receivedData[3] << 8;
             //   Console.WriteLine(x + ", " + y);
                    block = Tojpeg(receivedData);//constantly reciving blocks.
                    Draw(block, new Point(x, y));//applying the changes-drawing the block on the big initial image.using native memcpy.
            
               
                    // Invoke the update action, passing the updated block rectangle
                    BlockRectangle.X = x;
                    BlockRectangle.Y = y;
                    BlockRectangle.Width = block.Width;
                    BlockRectangle.Height = block.Height;
                    this.Invoke(updateAction);
               
            }
        }

     
        private Bitmap Tojpeg(byte[]buffer)
        {
            using (MemoryStream ms =new MemoryStream(buffer,4,buffer.Length-4))
            {
                
                return (Bitmap)Image.FromStream(ms);
            }
        }
        Rectangle viewRect;
        float scaleXRev,scaleYRev,scaleX, scaleY;
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {

            lock (initial)
            {         
                var targetRect = e.ClipRectangle;

                //Console.WriteLine(scaleX + " , " + scaleY);
                //MessageBox.Show(e.Graphics.SmoothingMode.ToString());

                var imageRect = new RectangleF(targetRect.X * scaleXRev, targetRect.Y * scaleYRev, targetRect.Width * scaleXRev, targetRect.Height * scaleYRev);
                //e.Graphics.FillRectangle(Brushes.Red,new Rectangle(targetRect.X-1,targetRect.Y-1,targetRect.Width-1,targetRect.Height-1));
                //Console.WriteLine(targetRect.ToString()+" ===>"+imageRect.ToString());
                //Console.WriteLine(targetRect);
                e.Graphics.DrawImage(initial, targetRect, imageRect, GraphicsUnit.Pixel);
            }
            
        }
        int count = 0;

        UdpClient udpclient;
        IPEndPoint ep;
        private void button1_Click(object sender, EventArgs e)
        {
            
             client = new TcpClient(textBox1.Text, 25655);
             sck = client.Client;          
             udpclient = new UdpClient();
             ep = new IPEndPoint(IPAddress.Parse(textBox1.Text), 1100); // endpoint where server is listening
             udpclient.Connect(ep);
             Thread.Sleep(500);
             udpclient.Send(new byte[] { 1 }, 1);
             panel1.Visible = false;
             pictureBox1.Visible = true;
             thread = new Thread(MainScreenThread);
             thread.Start();
        }



        private void timer1_Tick(object sender, EventArgs e)
        {

            //  this.Text = m.ToString() +"MBP/S";

            //mbs = 0;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
           
        }


    }
}
        /// </summary>
        public static class NativeMethods
        {

            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe int memcmp(byte* ptr1, byte* ptr2, uint count);

            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int memcmp(IntPtr ptr1, IntPtr ptr2, uint count);

            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int memcpy(IntPtr dst, IntPtr src, uint count);

            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe int memcpy(void* dst, void* src, uint count);
        }
    

