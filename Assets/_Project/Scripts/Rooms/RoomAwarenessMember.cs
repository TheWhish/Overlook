using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomAwarenessMember : MonoBehaviour
{
    private readonly List<RoomAwarenessZone> zones = new List<RoomAwarenessZone>();

    public RoomAwarenessZone CurrentZone => zones.Count > 0 ? zones[zones.Count - 1] : null;
    public bool HasZone => CurrentZone != null;

    public void EnterZone(RoomAwarenessZone zone)
    {
        if (zone == null || zones.Contains(zone))
        {
            return;
        }

        zones.Add(zone);
    }

    public void ExitZone(RoomAwarenessZone zone)
    {
        if (zone == null)
        {
            return;
        }

        zones.Remove(zone);
    }

    public bool SharesZoneWith(RoomAwarenessMember other)
    {
        return other != null && CurrentZone != null && CurrentZone == other.CurrentZone;
    }

    public void RefreshCurrentZone()
    {
        RemoveMissingZones();

        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
            {
                continue;
            }

            RoomAwarenessZone zone = hit.GetComponent<RoomAwarenessZone>();

            if (zone != null)
            {
                EnterZone(zone);
                return;
            }
        }
    }

    private void RemoveMissingZones()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            if (zones[i] == null)
            {
                zones.RemoveAt(i);
            }
        }
    }

    private void OnDisable()
    {
        zones.Clear();
    }
}
