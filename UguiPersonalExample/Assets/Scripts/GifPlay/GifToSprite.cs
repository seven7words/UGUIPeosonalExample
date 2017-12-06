using UnityEngine;

using System.Collections.Generic;

using System.Drawing;

using System.Drawing.Imaging;



public class GifToSprite : MonoBehaviour

{

    private List<Texture2D> _mTexture2Ds = new List<Texture2D>();

    private float _mTime;

    private float _mSpeed = 5.0f;



    UnityEngine.UI.Image gifTex;

    // Use this for initialization

    void Start()

    {

        gifTex = GetComponent<UnityEngine.UI.Image>();



        Image image = Image.FromFile(Application.dataPath + "/StreamingAssets/GIF.gif");

        _mTexture2Ds = GifToTextureByCS(image);

    }



    // Update is called once per frame

    void Update()

    {

        if (null != gifTex && _mTexture2Ds.Count > 0)

        {

            _mTime += 3 * Time.deltaTime;

            int index = (int)(_mTime * _mSpeed) % _mTexture2Ds.Count;

            //new Rect(0, 0, 769, 239)后面两个数字是图片的分辨率

            gifTex.overrideSprite = Sprite.Create(_mTexture2Ds[index], new Rect(0, 0, 240, 240), Vector2.zero);

        }

    }



    List<Texture2D> GifToTextureByCS(Image image)

    {

        List<Texture2D> texture2D = null;

        if (null != image)

        {

            texture2D = new List<Texture2D>();

            //Debug.LogError(image.FrameDimensionsList.Length);

            //image.FrameDimensionsList.Length = 1;

            //根据指定的唯一标识创建一个提供获取图形框架维度信息的实例;

            FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);

            //获取指定维度的帧数;

            int framCount = image.GetFrameCount(frameDimension);

            for (int i = 0; i < framCount; i++)

            {

                //选择由维度和索引指定的帧;

                image.SelectActiveFrame(frameDimension, i);

                var framBitmap = new Bitmap(image.Width, image.Height);

                //从指定的Image 创建新的Graphics,并在指定的位置使用原始物理大小绘制指定的 Image;

                //将当前激活帧的图形绘制到framBitmap上;

                System.Drawing.Graphics.FromImage(framBitmap).DrawImage(image, Point.Empty);

                var frameTexture2D = new Texture2D(framBitmap.Width, framBitmap.Height);

                for (int x = 0; x < framBitmap.Width; x++)

                {

                    for (int y = 0; y < framBitmap.Height; y++)

                    {

                        //获取当前帧图片像素的颜色信息;

                        System.Drawing.Color sourceColor = framBitmap.GetPixel(x, y);

                        //设置Texture2D上对应像素的颜色信息;

                        frameTexture2D.SetPixel(x, framBitmap.Height - 1 - y, new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A));

                    }

                }

                frameTexture2D.Apply();

                texture2D.Add(frameTexture2D);

            }

        }

        return texture2D;

    }

}