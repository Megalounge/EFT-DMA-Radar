/*
 * Quest Helper - Quest Zone
 * Based on analysis of eft-dma-radar_newUI_archive
 * Adapted for IL2CPP
 * 
 * Represents a quest zone that can be drawn on the radar.
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Represents a Quest Zone (PlaceItemTrigger, QuestTrigger, etc.)
    /// </summary>
    public sealed class QuestZone : IMapEntity, IMouseoverEntity
    {
        private Vector3 _position;

        /// <summary>
        /// Zone ID (matches quest condition zoneId).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Zone description/name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Quest name for display purposes.
        /// </summary>
        public string QuestName { get; set; }

        /// <summary>
        /// World position of the zone center.
        /// </summary>
        public ref readonly Vector3 Position => ref _position;

        /// <summary>
        /// Zone type (PlaceItem, Quest, etc.)
        /// </summary>
        public QuestZoneType ZoneType { get; }

        /// <summary>
        /// Whether this zone is active for a current quest.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether this zone objective has been completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Associated Quest ID (if any).
        /// </summary>
        public string QuestId { get; set; }

        /// <summary>
        /// Memory address of the zone object (can be updated when found in memory).
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// Mouse-over position for radar interaction.
        /// </summary>
        public Vector2 MouseoverPosition { get; set; }

        public QuestZone(ulong address, string id, Vector3 position, QuestZoneType type)
        {
            Address = address;
            Id = id;
            Name = id; // Default to ID, will be updated if name is available
            _position = position;
            ZoneType = type;
            IsActive = false;
            IsCompleted = false;
        }

        /// <summary>
        /// Update the position from memory (in case zone moves).
        /// </summary>
        public void UpdatePosition(Vector3 newPosition)
        {
            _position = newPosition;
        }

        /// <summary>
        /// Draw the quest zone on the radar.
        /// </summary>
        public void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            // Skip if not active or already completed
            if (!IsActive || IsCompleted)
                return;

            // Check draw distance
            var dist = Vector3.Distance(localPlayer.Position, Position);
            var maxDistance = App.Config.QuestHelper.ZoneDrawDistance;
            if (maxDistance > 0 && dist > maxDistance)
                return;

            var heightDiff = Position.Y - localPlayer.Position.Y;
            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);

            SKPaints.ShapeOutline.StrokeWidth = 2f;
            float distanceYOffset;
            float nameXOffset = 7f * App.Config.UI.UIScale;
            float nameYOffset;

            const float HEIGHT_INDICATOR_THRESHOLD = 1.85f;

            if (heightDiff > HEIGHT_INDICATOR_THRESHOLD) // Zone is above player
            {
                using var path = point.GetUpArrow(6);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.QuestHelperPaint);
                distanceYOffset = 18f * App.Config.UI.UIScale;
                nameYOffset = 6f * App.Config.UI.UIScale;
            }
            else if (heightDiff < -HEIGHT_INDICATOR_THRESHOLD) // Zone is below player
            {
                using var path = point.GetDownArrow(6);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.QuestHelperPaint);
                distanceYOffset = 12f * App.Config.UI.UIScale;
                nameYOffset = 1f * App.Config.UI.UIScale;
            }
            else // Zone is level with player
            {
                var size = 6 * App.Config.UI.UIScale;
                canvas.DrawCircle(point, size, SKPaints.ShapeOutline);
                canvas.DrawCircle(point, size, SKPaints.QuestHelperPaint);
                distanceYOffset = 16f * App.Config.UI.UIScale;
                nameYOffset = 4f * App.Config.UI.UIScale;
            }

            // Draw zone/quest name
            var displayName = !string.IsNullOrEmpty(QuestName) ? QuestName : Name;
            if (!string.IsNullOrEmpty(displayName) && displayName != Id)
            {
                var namePoint = new SKPoint(point.X + nameXOffset, point.Y + nameYOffset);
                canvas.DrawText(displayName, namePoint, SKPaints.TextOutline);
                canvas.DrawText(displayName, namePoint, SKPaints.QuestHelperText);
            }

            // Draw distance
            var distText = $"{(int)dist}m";
            var distWidth = SKPaints.QuestHelperText.MeasureText(distText);
            var distPoint = new SKPoint(
                point.X - (distWidth / 2),
                point.Y + distanceYOffset
            );
            canvas.DrawText(distText, distPoint, SKPaints.TextOutline);
            canvas.DrawText(distText, distPoint, SKPaints.QuestHelperText);
        }

        /// <summary>
        /// Draw mouseover tooltip for the quest zone.
        /// </summary>
        public void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            var lines = new List<string>();
            
            // Quest name
            if (!string.IsNullOrEmpty(QuestName))
                lines.Add($"Quest: {QuestName}");
            
            // Zone name/description
            if (!string.IsNullOrEmpty(Name) && Name != Id)
                lines.Add($"Zone: {Name}");
            else
                lines.Add($"Zone: {Id}");
            
            // Zone type
            lines.Add($"Type: {ZoneType}");
            
            // Distance
            float distance = Vector3.Distance(localPlayer.Position, Position);
            lines.Add($"Distance: {distance:F0}m");
            
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, lines.ToArray());
        }
    }

    /// <summary>
    /// Types of quest zones.
    /// </summary>
    public enum QuestZoneType
    {
        /// <summary>PlaceItemTrigger - Place item at location</summary>
        PlaceItem,
        /// <summary>QuestTrigger - Visit location</summary>
        Visit,
        /// <summary>Generic trigger zone</summary>
        Generic
    }
}
