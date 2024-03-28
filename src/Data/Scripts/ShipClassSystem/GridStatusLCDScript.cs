﻿using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Utils;
using VRageMath;
using IngameCubeBlock = VRage.Game.ModAPI.Ingame.IMyCubeBlock;
using IngameIMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

namespace ShipClassSystem.Data.Scripts.ShipClassSystem
{
    [MyTextSurfaceScript("GridStatusLCDScript", "Grid class status")]
    internal class GridStatusLCDScript : MyTSSCommon
    {
        private static readonly float ScrollSpeed = 3; //pixels per update

        private static readonly int
            ScrollPauseUpdates = 18; //how many updates to say paused at the start and end when scrolling

        private readonly Table _appliedModifiersTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "ModifierName" },
                new Column { Name = "Value" }
            }
        };

        private readonly Table _gridResultsTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "Property" },
                new Column { Name = "Value", Alignment = TextAlignment.RIGHT },
                new Column { Name = "Separator" },
                new Column { Name = "Max" },
                new Column { Name = "Success" }
            }
        };

        private readonly Table _headerTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "Label" },
                new Column { Name = "Name" },
                new Column { Name = "Success" }
            }
        };

        private readonly IMyTerminalBlock _terminalBlock;
        private readonly CubeGridLogic _attachedLogic;
        private int _scrollTime;

        public GridStatusLCDScript(IMyTextSurface surface, IngameCubeBlock block, Vector2 size) : base(surface, block,
            size)
        {
            _terminalBlock =
                (IMyTerminalBlock)block; // internal stored m_block is the ingame interface which has no events, so can't unhook later on, therefore this field is required.
            _terminalBlock.OnMarkForClose +=
                BlockMarkedForClose; // required if you're gonna make use of Dispose() as it won't get called when block is removed or grid is cut/unloaded.
            _attachedLogic = _terminalBlock.GetGridLogic();
            // Called when script is created.
            // This class is instanced per LCD that uses it, which means the same block can have multiple instances of this script aswell (e.g. a cockpit with all its screens set to use this script).
        }

        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update10; // frequency that Run() is called.

        private CubeGridLogic GridLogic => _terminalBlock?.GetGridLogic();

        public override void Dispose()
        {
            base.Dispose(); // do not remove
            _terminalBlock.OnMarkForClose -= BlockMarkedForClose;

            // Called when script is removed for any reason, so that you can clean up stuff if you need to.
        }

        private void BlockMarkedForClose(IngameIMyEntity ent)
        {
            Dispose();
        }

        // gets called at the rate specified by NeedsUpdate
        // it can't run every tick because the LCD is capped at 6fps anyway.
        public override void Run()
        {
            try
            {
                base.Run(); // do not remove

                /*// hold L key to see how the error is shown, remove this after you've played around with it =)
                if (MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.L))
                    throw new Exception("Oh noes an error :}");*/

                Draw();
            }
            catch (Exception
                   e) // no reason to crash the entire game just for an LCD script, but do NOT ignore them either, nag user so they report it :}
            {
                DrawError(e);
            }
        }

        private void Draw() // this is a custom method which is called in Run().
        {
            if (!Constants.IsClient) return;
            Utils.Log("HIT1");
            var screenSize = Surface.SurfaceSize;
            var screenTopLeft = (Surface.TextureSize - screenSize) * 0.5f;
            var padding = new Vector2(10, 10);
            var cellGap = new Vector2(12, 5);
            var screenInnerWidth = Surface.SurfaceSize.X - padding.X * 2;
            var successColor = Color.Green;
            var failColor = Color.Red;
            const float baseScale = 1.25f;
            var bodyScale = baseScale * 13 / TextUtils.CharWidth;

            var frame = Surface.DrawFrame();

            // https://github.com/malware-dev/MDK-SE/wiki/Text-Panels-and-Drawing-Sprites

            AddBackground(frame, Color.White.Alpha(0.05f));

            // the colors in the terminal are Surface.ScriptBackgroundColor and Surface.ScriptForegroundColor, the other ones without Script in name are for text/image mode.
            var gridClass = _terminalBlock.GetGridLogic().GridClass;
            var concreteGrid = _terminalBlock.CubeGrid as MyCubeGrid;

            if (gridClass == null || concreteGrid == null) return;

            _gridResultsTable.Clear();

            Vector2 currentPosition;
            var spritesToRender = new List<MySprite>();

            //Render the header
            _headerTable.Clear();

            _headerTable.Rows.Add(new Row
            {
                new Cell("Class:"),
                new Cell(gridClass.Name)
            });

            _headerTable.RenderToSprites(spritesToRender, screenTopLeft + padding, screenInnerWidth, new Vector2(15, 0),
                out currentPosition, baseScale);
            Utils.Log("HIT2");
            _gridResultsTable.Rows.Add(new Row
            {
                new Cell("Blocks: "),
                new Cell(concreteGrid.BlocksCount.ToString()),
                new Cell("/"),
                new Cell(gridClass.MaxBlocks.ToString())
            });

            if (gridClass.MaxMass > 0)
                _gridResultsTable.Rows.Add(new Row
                {
                    new Cell("Mass: "),
                    new Cell(Math.Round(concreteGrid.Mass).ToString(CultureInfo.InvariantCulture)),
                    new Cell("/"),
                    new Cell(gridClass.MaxMass.ToString(CultureInfo.InvariantCulture))
                });

            if (gridClass.MaxPCU > 0)
                _gridResultsTable.Rows.Add(new Row
                {
                    new Cell("PCU: "),
                    new Cell(concreteGrid.BlocksPCU.ToString()),
                    new Cell("/"),
                    new Cell(gridClass.MaxPCU.ToString())
                });

            if (gridClass.BlockLimits != null)
                foreach (var blockLimit in gridClass.BlockLimits)
                {
                    var relevantBlocks = _attachedLogic.Blocks.Where(b => blockLimit.BlockTypes
                        .Any(bl => bl.SubtypeId == Utils.GetBlockSubtypeId(b) &&
                                   bl.TypeId == Utils.GetBlockId(b))).ToList();

                    _gridResultsTable.Rows.Add(new Row
                    {
                        new Cell($"{blockLimit.Name}:"),
                        new Cell(relevantBlocks.ToString()),
                        new Cell("/"),
                        new Cell(blockLimit.MaxCount.ToString(CultureInfo.InvariantCulture), relevantBlocks.Count > blockLimit.MaxCount ? successColor : failColor),
                        relevantBlocks.Count > blockLimit.MaxCount ? new Cell() : new Cell("X", failColor)
                    });
                }

            var gridResultsTableTopLeft = currentPosition + new Vector2(0, 5);

            _gridResultsTable.RenderToSprites(spritesToRender, gridResultsTableTopLeft, screenInnerWidth, cellGap,
                out currentPosition, bodyScale);
            Utils.Log("HIT3");
            //Applied modifiers
            spritesToRender.Add(CreateLine("Applied modifiers", currentPosition + new Vector2(0, 5), out currentPosition,
                baseScale));

            _appliedModifiersTable.Clear();

            var appliedModifiersTableTopLeft = currentPosition + new Vector2(0, 5);

            foreach (var modifierValue in GridLogic.Modifiers.GetModifierValues())
                _appliedModifiersTable.Rows.Add(new Row
                {
                    new Cell($"{modifierValue.Name}:"),
                    new Cell(modifierValue.Value.ToString())
                });

            _appliedModifiersTable.RenderToSprites(spritesToRender, appliedModifiersTableTopLeft, screenInnerWidth,
                cellGap, out currentPosition, bodyScale);
            Utils.Log("HIT4");
            var scrollPosition = GetScrollPosition(currentPosition + padding);

            foreach (var t in spritesToRender)
            {
                var sprite = t;

                if (scrollPosition.Y != 0) sprite.Position -= scrollPosition;

                frame.Add(sprite);
            }

            frame.Dispose(); // send sprites to the screen
        }

        private Vector2 GetScrollPosition(Vector2 contentBottomRight)
        {
            var screenSize = Surface.SurfaceSize;
            var screenTopLeft = (Surface.TextureSize - screenSize) * 0.5f;
            var contentHeight = contentBottomRight.Y - screenTopLeft.Y;

            if (!(contentHeight > screenSize.Y)) return new Vector2();
            var scrollRange = contentHeight - screenSize.Y;
            var numUpdatesToScroll = (int)Math.Ceiling(scrollRange / ScrollSpeed);
            var fullScrollCycleTime = (ScrollPauseUpdates + numUpdatesToScroll) * 2;
            Vector2 scrollPosition;

            if (_scrollTime < ScrollPauseUpdates)
                scrollPosition = new Vector2();
            else if (_scrollTime < ScrollPauseUpdates + numUpdatesToScroll)
                scrollPosition = new Vector2(0, (_scrollTime - ScrollPauseUpdates) * ScrollSpeed);
            else if (_scrollTime < ScrollPauseUpdates * 2 + numUpdatesToScroll)
                scrollPosition = new Vector2(0, scrollRange);
            else
                scrollPosition = new Vector2(0,
                    scrollRange - (_scrollTime - (ScrollPauseUpdates * 2 + numUpdatesToScroll)) * ScrollSpeed);

            _scrollTime++;

            if (_scrollTime > fullScrollCycleTime) _scrollTime = 0;

            return scrollPosition;

        }

        private MySprite CreateLine(string text, Vector2 position, float scale = 1)
        {
            var ignored = new Vector2();

            return CreateLine(text, position, out ignored, scale);
        }

        private static MySprite CreateLine(string text, Vector2 position, out Vector2 positionAfter, float scale = 1)
        {
            var sprite = MySprite.CreateText(text, "Monospace", Color.White, scale, TextAlignment.LEFT);
            sprite.Position =
                position; // screenCorner + padding + new Vector2(0, y); // 16px from topleft corner of the visible surface

            positionAfter = position + new Vector2(0, TextUtils.GetTextHeight(text, scale));

            return sprite;
        }

        private void DrawError(Exception e)
        {
            Utils.Log($"{e.Message}\n{e.StackTrace}", 3);

            try // first try printing the error on the LCD
            {
                var screenSize = Surface.SurfaceSize;
                var screenCorner = (Surface.TextureSize - screenSize) * 0.5f;

                var frame = Surface.DrawFrame();

                var bg = new MySprite(SpriteType.TEXTURE, "SquareSimple", null, null, Color.Black);
                frame.Add(bg);

                var text = MySprite.CreateText(
                    $"ERROR: {e.Message}\n{e.StackTrace}\n\nPlease send screenshot of this to mod author.\n{MyAPIGateway.Utilities.GamePaths.ModScopeName}",
                    "White", Color.Red, 0.7f, TextAlignment.LEFT);
                text.Position = screenCorner + new Vector2(16, 16);
                frame.Add(text);

                frame.Dispose();
            }
            catch (Exception e2)
            {
                MyLog.Default.WriteLineAndConsole(
                    $"Also failed to draw error on screen: {e2.Message}\n{e2.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification(
                        $"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000,
                        MyFontEnum.Red);
            }
        }
    }
}