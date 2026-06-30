using MediaBrowser.Controller.Entities;
using ReTrak.Model.Enums;

namespace ReTrak.Model;

internal sealed class LibraryEvent
{
    public BaseItem Item { get; set; }

    public ReTrakUser ReTrakUser { get; set; }

    public EventType EventType { get; set; }
}
