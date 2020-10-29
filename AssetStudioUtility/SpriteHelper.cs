using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public static class SpriteHelper
    {
        public static Bitmap GetImage(this Sprite m_Sprite, bool useAlpha, Texture2D chrAlphaTex = null, bool hq = false)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
            {
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
                {
                    return CutImage(m_Texture2D, m_Sprite, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.settingsRaw);
                }
            }
            else
            {
                if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D) && chrAlphaTex != null)
                {
                    var bitmapTex = m_Texture2D.ConvertToBitmap(true);
                    using (bitmapTex)
                    {
                        var bitmapAlpha = chrAlphaTex.ConvertToBitmap(true);
                        if (bitmapAlpha.Size != bitmapTex.Size)
                            bitmapAlpha = Resize(bitmapAlpha, new Rectangle(0, 0, bitmapTex.Width, bitmapTex.Height), hq);
                        return ApplyMask(bitmapTex, bitmapAlpha);
                    }
                }
                else if (useAlpha && m_Sprite.m_RD.texture.TryGet(out m_Texture2D) && m_Sprite.m_RD.alphaTexture.TryGet(out var m_TexAlpha2D))
                {
                    var tex = CutImage(m_Texture2D, m_Sprite, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.settingsRaw);
                    var texAlpha = CutImage(m_TexAlpha2D, m_Sprite, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.settingsRaw);
                    return ApplyMask(tex, texAlpha);
                }
                else if (m_Sprite.m_RD.texture.TryGet(out m_Texture2D))
                {
                    return CutImage(m_Texture2D, m_Sprite, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.settingsRaw);
                }
            }
            return null;
        }

        private static Bitmap CutImage(Texture2D m_Texture2D, Sprite m_Sprite, RectangleF textureRect, Vector2 textureRectOffset, SpriteSettings settingsRaw)
        {
            var originalImage = m_Texture2D.ConvertToBitmap(false);
            if (originalImage != null)
            {
                using (originalImage)
                {
                    //var spriteImage = originalImage.Clone(textureRect, PixelFormat.Format32bppArgb);
                    var textureRectI = Rectangle.Round(textureRect);
                    if (textureRectI.Width == 0)
                    {
                        textureRectI.Width = 1;
                    }
                    if (textureRectI.Height == 0)
                    {
                        textureRectI.Height = 1;
                    }
                    var spriteImage = new Bitmap(textureRectI.Width, textureRectI.Height, PixelFormat.Format32bppArgb);
                    var destRect = new Rectangle(0, 0, textureRectI.Width, textureRectI.Height);
                    using (var graphic = Graphics.FromImage(spriteImage))
                    {
                        graphic.DrawImage(originalImage, destRect, textureRectI, GraphicsUnit.Pixel);
                    }
                    if (settingsRaw.packed == 1)
                    {
                        //RotateAndFlip
                        switch (settingsRaw.packingRotation)
                        {
                            case SpritePackingRotation.kSPRFlipHorizontal:
                                spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipX);
                                break;
                            case SpritePackingRotation.kSPRFlipVertical:
                                spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
                                break;
                            case SpritePackingRotation.kSPRRotate180:
                                spriteImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                break;
                            case SpritePackingRotation.kSPRRotate90:
                                spriteImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                break;
                        }
                    }

                    //Tight
                    if (settingsRaw.packingMode == SpritePackingMode.kSPMTight)
                    {
                        try
                        {
                            var triangles = GetTriangles(m_Sprite.m_RD);
                            var points = triangles.Select(x => x.Select(y => new PointF(y.X, y.Y)).ToArray());
                            using (var path = new GraphicsPath())
                            {
                                foreach (var p in points)
                                {
                                    path.AddPolygon(p);
                                }
                                using (var matr = new Matrix())
                                {
                                    var version = m_Sprite.version;
                                    if (version[0] < 5
                                       || (version[0] == 5 && version[1] < 4)
                                       || (version[0] == 5 && version[1] == 4 && version[2] <= 1)) //5.4.1p3 down
                                    {
                                        matr.Translate(m_Sprite.m_Rect.Width * 0.5f - textureRectOffset.X, m_Sprite.m_Rect.Height * 0.5f - textureRectOffset.Y);
                                    }
                                    else
                                    {
                                        matr.Translate(m_Sprite.m_Rect.Width * m_Sprite.m_Pivot.X - textureRectOffset.X, m_Sprite.m_Rect.Height * m_Sprite.m_Pivot.Y - textureRectOffset.Y);
                                    }
                                    matr.Scale(m_Sprite.m_PixelsToUnits, m_Sprite.m_PixelsToUnits);
                                    path.Transform(matr);
                                    var bitmap = new Bitmap(textureRectI.Width, textureRectI.Height);
                                    using (var graphic = Graphics.FromImage(bitmap))
                                    {
                                        using (var brush = new TextureBrush(spriteImage))
                                        {
                                            graphic.FillPath(brush, path);
                                            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                                            return bitmap;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    //Rectangle
                    spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    return spriteImage;
                }
            }

            return null;
        }

        private static Bitmap ApplyMask(Bitmap imageTex, Bitmap imageMask)
        {
            var imageOut = new Bitmap(imageTex.Width, imageTex.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, imageTex.Width, imageTex.Height);
            var imageMaskData = imageMask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var imageTexData = imageTex.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var imageOutData = imageOut.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                for (int y = 0; y < imageTex.Height; y++)
                {
                    byte* ptrMask = (byte*)imageMaskData.Scan0 + y * imageMaskData.Stride;
                    byte* ptrTex = (byte*)imageTexData.Scan0 + y * imageTexData.Stride;
                    byte* ptrOut = (byte*)imageOutData.Scan0 + y * imageOutData.Stride;
                    for (int x = 0; x < imageTex.Width; x++)
                    {
                        ptrOut[4 * x] = ptrTex[4 * x];          //B
                        ptrOut[4 * x + 1] = ptrTex[4 * x + 1];  //G
                        ptrOut[4 * x + 2] = ptrTex[4 * x + 2];  //R
                        ptrOut[4 * x + 3] = ptrMask[4 * x];     //A (from B in mask)
                    }
                }
            }
            imageMask.UnlockBits(imageMaskData);
            imageTex.UnlockBits(imageTexData);
            imageOut.UnlockBits(imageOutData);
            return imageOut;
        }

        private static Bitmap Resize(Bitmap image, Rectangle dstRect, bool hq)
        {
            var quality = (CQ:CompositingQuality.HighSpeed, IM:InterpolationMode.NearestNeighbor, SM:SmoothingMode.None);
            if (hq)
                quality = (CQ:CompositingQuality.HighQuality, IM:InterpolationMode.HighQualityBicubic, SM:SmoothingMode.HighQuality);
            var imageOut = new Bitmap(dstRect.Width, dstRect.Height, PixelFormat.Format32bppArgb);
            using (var graphic = Graphics.FromImage(imageOut))
            {
                graphic.CompositingMode = CompositingMode.SourceCopy;
                graphic.CompositingQuality = quality.CQ;
                graphic.InterpolationMode = quality.IM;
                graphic.SmoothingMode = quality.SM;
                var srcRect = new Rectangle(0, 0, image.Width, image.Height);
                graphic.DrawImage(image, dstRect, srcRect, GraphicsUnit.Pixel);
            }
            return imageOut;
        }

        private static Vector2[][] GetTriangles(SpriteRenderData m_RD)
        {
            if (m_RD.vertices != null) //5.6 down
            {
                var vertices = m_RD.vertices.Select(x => (Vector2)x.pos).ToArray();
                var triangleCount = m_RD.indices.Length / 3;
                var triangles = new Vector2[triangleCount][];
                for (int i = 0; i < triangleCount; i++)
                {
                    var first = m_RD.indices[i * 3];
                    var second = m_RD.indices[i * 3 + 1];
                    var third = m_RD.indices[i * 3 + 2];
                    var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                    triangles[i] = triangle;
                }
                return triangles;
            }

            return GetTriangles(m_RD.m_VertexData, m_RD.m_SubMeshes, m_RD.m_IndexBuffer); //5.6 and up
        }

        private static Vector2[][] GetTriangles(VertexData m_VertexData, SubMesh[] m_SubMeshes, byte[] m_IndexBuffer)
        {
            var triangles = new List<Vector2[]>();
            var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
            var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
            using (BinaryReader vertexReader = new BinaryReader(new MemoryStream(m_VertexData.m_DataSize)),
                                indexReader = new BinaryReader(new MemoryStream(m_IndexBuffer)))
            {
                foreach (var subMesh in m_SubMeshes)
                {
                    vertexReader.BaseStream.Position = m_Stream.offset + subMesh.firstVertex * m_Stream.stride + m_Channel.offset;

                    var vertices = new Vector2[subMesh.vertexCount];
                    for (int v = 0; v < subMesh.vertexCount; v++)
                    {
                        vertices[v] = vertexReader.ReadVector3();
                        vertexReader.BaseStream.Position += m_Stream.stride - 12;
                    }

                    indexReader.BaseStream.Position = subMesh.firstByte;

                    var triangleCount = subMesh.indexCount / 3u;
                    for (int i = 0; i < triangleCount; i++)
                    {
                        var first = indexReader.ReadUInt16() - subMesh.firstVertex;
                        var second = indexReader.ReadUInt16() - subMesh.firstVertex;
                        var third = indexReader.ReadUInt16() - subMesh.firstVertex;
                        var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                        triangles.Add(triangle);
                    }
                }
            }
            return triangles.ToArray();
        }
    }
}
