using UnityEngine;
using UnityEditor;
using System.IO;

namespace RollingEgg.EditorTools
{
    /// <summary>
    /// 스프라이트 시트에서 개별 스프라이트를 PNG 파일로 추출하는 에디터 유틸리티입니다.
    /// 프로젝트 뷰에서 스프라이트(자식 에셋)를 우클릭하고 "Extract Sprite to PNG"를 선택하세요.
    /// </summary>
    public class SpriteExtractor
    {
        [MenuItem("Assets/Extract Sprite to PNG", false, 20)]
        public static void ExtractSprite()
        {
            Object[] selectedObjects = Selection.objects;

            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("선택된 스프라이트가 없습니다.");
                return;
            }

            int successCount = 0;

            foreach (Object obj in selectedObjects)
            {
                if (obj is Sprite sprite)
                {
                    if (SaveSpriteAsPNG(sprite))
                    {
                        successCount++;
                    }
                }
            }
            
            if (successCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"<b>[SpriteExtractor]</b> 총 {successCount}개의 스프라이트를 성공적으로 추출했습니다.");
            }
        }

        // 선택한 항목 중에 스프라이트가 있을 때만 메뉴 활성화
        [MenuItem("Assets/Extract Sprite to PNG", true)]
        public static bool ValidateExtractSprite()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is Sprite) return true;
            }
            return false;
        }

        private static bool SaveSpriteAsPNG(Sprite sprite)
        {
            Texture2D sourceTex = sprite.texture;
            if (sourceTex == null) return false;

            string assetPath = AssetDatabase.GetAssetPath(sourceTex);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null) return false;

            bool wasReadable = importer.isReadable;

            // 텍스처 읽기 권한이 없으면 임시로 활성화
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            try
            {
                Rect r = sprite.textureRect;

                // 새 텍스처 생성 (투명도 포함)
                Texture2D newTex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                
                // 원본 텍스처에서 픽셀 데이터 가져오기
                Color[] pixels = sourceTex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                newTex.SetPixels(pixels);
                newTex.Apply();

                // PNG로 인코딩 및 저장
                byte[] bytes = newTex.EncodeToPNG();
                string dir = Path.GetDirectoryName(assetPath);
                string savePath = Path.Combine(dir, $"{sprite.name}_extracted.png");

                // 파일명 중복 처리
                int counter = 1;
                while (File.Exists(savePath))
                {
                    savePath = Path.Combine(dir, $"{sprite.name}_extracted_{counter}.png");
                    counter++;
                }

                File.WriteAllBytes(savePath, bytes);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpriteExtractor] 추출 실패 ({sprite.name}): {e.Message}");
                return false;
            }
            finally
            {
                // 원래 설정으로 복구 (선택 사항: 매번 리임포트하는게 번거로우면 이 부분을 주석 처리해도 됩니다)
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}

