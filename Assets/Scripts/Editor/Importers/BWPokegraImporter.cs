using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pokemon.Importers.DS.Narc;
using Pokemon.Importers.DS.Narc.Entries;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AssetImporters;
using UnityEngine.Rendering;

[ScriptedImporter(1, "narc")]
public class BWPokegraImporter : ScriptedImporter
{
    public int pokeId = 84;
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var fileStream = new FileStream(ctx.assetPath, FileMode.Open);
        var narc = NarcFile.Parse(fileStream);
        fileStream.Close();

        for (int i = narc.Entries.Count - 17; i >= 2; i--)
        {
            var entry = narc.Entries[i];
            var cells = GetAtOrAsChild<CellsNarcEntry>(entry);
            var tiles = GetAtOrAsChild<TilesNarcEntry>(narc.Entries[i - 2]);
            var palette = GetAtOrAsChild<PaletteNarcEntry>(narc.Entries[i + 14]);
            var anim = GetAtOrAsChild<AnimationResourceNarcEntry>(narc.Entries[i + 1]);
            var mcr = GetAtOrAsChild<MappedCellResourceEntry>(narc.Entries[i + 2]);
            if (i == (pokeId-1) * 20 + 24) Debug.Log($"{cells} {tiles} {palette}");
            else continue;
            if (cells == null || tiles == null || palette == null) continue;
            var t = tiles.BuildTileSheet(palette.Palettes[0]);
            t.name = $"{tiles}";
            ctx.AddObjectToAsset(t.name, t);
            var rootGo = new GameObject(i.ToString());
            string[] cellPaths = new string[cells.Cells.Count];
            Sprite[] cellSprites = new Sprite[cells.Cells.Count];
            for (var c = 0; c < cells.Cells.Count; c++)
            {
                var cellGo = new GameObject(c.ToString());
                cellPaths[c] = cellGo.name;
                cellGo.transform.SetParent(rootGo.transform);
                var cell = cells.Cells[c];
                for (var o = 0; o < cell.Oams.Length; o++)
                {
                    var cO = cell.Oams[o];
                    var spr = Sprite.Create(t,
                        new Rect(cO.TileIndex % tiles.WidthTiles * 8,
                            t.height - (cO.TileIndex / tiles.WidthTiles * 8) - cO.Height,
                            cO.Width, cO.Height), Vector2.zero, 1);
                    spr.name = $"{i}_c{c}_o{o}";
                    if (o == 0) cellSprites[c] = spr;
                    ctx.AddObjectToAsset(spr.name, spr);
                    var sprGo = cellGo;
                    if (cell.Oams.Length > 1)
                    {
                        sprGo = new GameObject(o.ToString());
                        sprGo.transform.SetParent(cellGo.transform);
                    }
                    var sprRend = sprGo.AddComponent<SpriteRenderer>();
                    sprRend.sortingOrder = cO.Priority;
                    //sprRend.transform.localPosition = new Vector3(cell.Bounds.xMin / 32F, -cell.Bounds.yMin / 32F, 0F);
                    sprRend.sprite = spr;
                }

                //var result = cell.Render(tiles, palette.Palettes[0]);
                //result.texture.name = $"{cells}_{c}";
                //ctx.AddObjectToAsset(result.texture.name, result.texture);
            }

            if (mcr != null && anim != null)
            {
                for (var animationIdx = 0; animationIdx < mcr.Animations.Length; animationIdx++)
                {
                    var mappedAnimation = mcr.Animations[animationIdx];
                    var animClip = new AnimationClip
                    {
                        name = $"mcr_{animationIdx}"
                    };

                    for (var animationCellIdx = 0;
                         animationCellIdx < mappedAnimation.AnimationCells.Length;
                         animationCellIdx++)
                    {
                        var mappedCell = mappedAnimation.AnimationCells[animationCellIdx];
                        var animationCell = anim.Cells[mappedCell.AnimationCellIndex];
                        
                        Keyframe[] posX = new Keyframe[animationCell.Frames.Length],
                            posY = new Keyframe[animationCell.Frames.Length],
                            qX = new Keyframe[animationCell.Frames.Length],
                            qY = new Keyframe[animationCell.Frames.Length],
                            qZ = new Keyframe[animationCell.Frames.Length],
                            qW = new Keyframe[animationCell.Frames.Length];
                        
                        var sprite = new List<ObjectReferenceKeyframe>(animationCell.Frames.Length);
                        int cellIdx = animationCell.Frames[0].CellIndex;
                        float time = 0F, prevTime = 0F;
                        for (int f = 0; f < animationCell.Frames.Length; f++)
                        {
                            var frame = animationCell.Frames[f];
                            posX[f].time = posY[f].time = time;
                            posX[f].value = frame.X + mappedCell.X;
                            posY[f].value = -(frame.Y + mappedCell.Y);
                            
                            var q = Quaternion.Euler(0F, 0F, -frame.Theta / 65535F * Mathf.Rad2Deg * 4);
                            qX[f].time = qY[f].time = qZ[f].time = qW[f].time = time;
                            qX[f].value = q.x;
                            qY[f].value = q.y;
                            qZ[f].value = q.z;
                            qW[f].value = q.w;
                            
                            if (frame.CellIndex != cellIdx) sprite.Add(new ObjectReferenceKeyframe()
                            {
                                time = time,
                                value = cellSprites[frame.CellIndex],
                            });
                            prevTime = time;
                            time += frame.FrameDuration / 60F;
                        }
                        string path = cellPaths[animationCell.Frames[0].CellIndex];
                        animClip.SetCurve(path, typeof(Transform), "localPosition.x", new AnimationCurve(posX));
                        animClip.SetCurve(path, typeof(Transform), "localPosition.y", new AnimationCurve(posY));
                        
                        if (animationCell.FrameType == 1)
                        {
                            animClip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", new AnimationCurve(qX));
                            animClip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", new AnimationCurve(qY));
                            animClip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", new AnimationCurve(qZ));
                            animClip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", new AnimationCurve(qW));
                        }
                        
                        if (sprite.Count > 0)
                        {
                            sprite.Insert(0, new ObjectReferenceKeyframe()
                            {
                                time = 0F,
                                value = cellSprites[animationCell.Frames[0].CellIndex],
                            });
                            AnimationUtility.SetObjectReferenceCurve(animClip, new EditorCurveBinding()
                            {
                                type = typeof(SpriteRenderer),
                                path = path,
                                propertyName = "m_Sprite"
                            }, sprite.ToArray());
                        };
                        animClip.SetCurve(path, typeof(SpriteRenderer), "m_SortingOrder", AnimationCurve.Constant(0F, prevTime, 0-(int)mappedCell.Priority));
                    }
                    
                    ctx.AddObjectToAsset(animClip.name, animClip);
                }
            }
            ctx.AddObjectToAsset(rootGo.name, rootGo);
            ctx.SetMainObject(rootGo);

            
            Debug.Log(entry.ToString());
        }
    }

    private T GetAtOrAsChild<T>(NarcEntry entry) where T : NarcEntry
    {
        return entry as T ?? (entry is CompositeNarcEntry composite
            ? composite.Children.OfType<T>().FirstOrDefault()
            : null);
    }
}
