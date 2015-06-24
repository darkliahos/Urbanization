﻿using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Windows.Forms;
using Mirage.Urbanization.Simulation.Datameters;
using Mirage.Urbanization.Tilesets;
using Mirage.Urbanization.WinForms.Rendering;
using Mirage.Urbanization.ZoneConsumption;
using Mirage.Urbanization.ZoneConsumption.Base;

namespace Mirage.Urbanization.WinForms
{
    public class RenderZoneContinuation
    {
        private readonly Action _drawSecondLayerAction;
        private readonly Action<IAreaConsumption> _drawHighligterAction;

        public void DrawSecondLayer()
        {
            if (_drawSecondLayerAction != null)
                _drawSecondLayerAction();
        }

        public void DrawHighlighter(IAreaConsumption consumption)
        {
            if (_drawHighligterAction != null)
                _drawHighligterAction(consumption);
        }

        public bool HasDrawHighlighterDelegate { get { return _drawHighligterAction != null; } }

        public bool HasDrawSecondLayerDelegate { get { return _drawSecondLayerAction != null; } }

        public RenderZoneContinuation(Action drawSecondLayerAction, Action<IAreaConsumption> drawHighligterAction)
        {
            _drawSecondLayerAction = drawSecondLayerAction;
            _drawHighligterAction = drawHighligterAction;
        }
    }

    public class ZoneRenderInfo
    {
        private readonly IReadOnlyZoneInfo _zoneInfo;
        private readonly Func<IReadOnlyZoneInfo, Rectangle> _createRectangle;
        private readonly ITilesetAccessor _tilesetAccessor;
        private readonly RenderZoneOptions _renderZoneOptions;

        public IReadOnlyZoneInfo ZoneInfo { get { return _zoneInfo; } }

        public Rectangle GetRectangle()
        {
            return _createRectangle(_zoneInfo);
        }

        public ZoneRenderInfo(IReadOnlyZoneInfo zoneInfo, Func<IReadOnlyZoneInfo, Rectangle> createRectangle, ITilesetAccessor tilesetAccessor, RenderZoneOptions renderZoneOptions)
        {
            _zoneInfo = zoneInfo;
            _createRectangle = createRectangle;
            _tilesetAccessor = tilesetAccessor;
            _renderZoneOptions = renderZoneOptions;
        }

        public RenderZoneContinuation RenderZoneInto(IGraphicsWrapper graphics, bool isHighlighted)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");

            var rectangle = GetRectangle();

            var consumption = ZoneInfo.ZoneConsumptionState.GetZoneConsumption();

            Action drawSecondLayerAction = null;

            BitmapLayer bitmapLayer;
            if (_tilesetAccessor.TryGetBitmapFor(consumption, out bitmapLayer))
            {
                graphics.DrawImage(bitmapLayer.LayerOne, rectangle);

                if (bitmapLayer.IsLayerTwoSpecified)
                    drawSecondLayerAction = () => { graphics.DrawImage(bitmapLayer.LayerTwo, rectangle); };

                if (_renderZoneOptions.ShowDebugGrowthPathFinding)
                {
                    switch (_zoneInfo.GrowthAlgorithmHighlightState.Current)
                    {
                        case HighlightState.UsedAsPath:
                            graphics.DrawRectangle(BrushManager.GreenPen, rectangle);
                            break;
                        case HighlightState.Examined:
                            graphics.DrawRectangle(BrushManager.YellowPen, rectangle);
                            break;
                    }
                }
            }
            else
            {
                graphics.FillRectangle(BrushManager.Instance.GetBrushFor(consumption), rectangle);
            }

            var overlayOption = _renderZoneOptions.CurrentOverlayOption;
            if (overlayOption != null)
                overlayOption.Render(ZoneInfo, rectangle, graphics);

            if (_renderZoneOptions.ShowDebugAverageTravelDistances)
            {
                var averageTravelDistance = ZoneInfo.GetLastAverageTravelDistance();
                if (averageTravelDistance != 0)
                    graphics.DrawString(averageTravelDistance.ToString(),
                        BrushManager.ZoneInfoFont,
                        averageTravelDistance > 0 ? BrushManager.RedSolidBrush : BrushManager.BlackSolidBrush,
                        rectangle);
            }

            if (_renderZoneOptions.ShowDebugPopulationDensity)
            {
                var populationDensity = ZoneInfo.GetPopulationDensity();
                if (populationDensity != 0)
                    graphics.DrawString(populationDensity.ToString(),
                        BrushManager.ZoneInfoFont,
                        BrushManager.BlackSolidBrush,
                        rectangle);
            }

            if (_renderZoneOptions.RenderDebugLandValueValues)
            {
                var landValue = ZoneInfo.GetLastLandValueResult();
                if (landValue.HasMatch && landValue.MatchingObject.LandValueInUnits != 0)
                    graphics.DrawString(landValue.MatchingObject.LandValueInUnits.ToString(),
                        BrushManager.ZoneInfoFont,
                        BrushManager.BlackSolidBrush,
                        rectangle);
            }

            if (isHighlighted)
            {
                return new RenderZoneContinuation(
                    drawSecondLayerAction,

                (areaConsumption) =>
                {
                    if (areaConsumption is IAreaZoneClusterConsumption)
                    {
                        var sampleZones = (areaConsumption as IAreaZoneClusterConsumption)
                            .ZoneClusterMembers;

                        var width = sampleZones.GroupBy(x => x.RelativeToParentCenterX).Count() * _tilesetAccessor.TileWidthAndSizeInPixels;
                        var height = sampleZones.GroupBy(x => x.RelativeToParentCenterY).Count() * _tilesetAccessor.TileWidthAndSizeInPixels;

                        var xOffset = sampleZones.Min(x => x.RelativeToParentCenterX) * _tilesetAccessor.TileWidthAndSizeInPixels;
                        var yOffset = sampleZones.Min(x => x.RelativeToParentCenterY) * _tilesetAccessor.TileWidthAndSizeInPixels;

                        rectangle.Size = new Size(
                            width: width,
                            height: height
                        );

                        rectangle.Location = new Point(
                            x: rectangle.Location.X + xOffset, y: rectangle.Location.Y + yOffset);
                    }

                    var pen = (DateTime.Now.Millisecond % 400) > 200 ? BrushManager.BluePen : BrushManager.RedPen;
                    graphics.DrawRectangle(pen, rectangle);
                });
            }
            return new RenderZoneContinuation(drawSecondLayerAction, null);
        }
    }
}