using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Courtslot
{
    public int SlotId { get; set; }

    public int CourtId { get; set; }

    public int Slotnumber { get; set; }

    public DateTime Starttime { get; set; }

    public DateTime Endtime { get; set; }

    public bool Isavailable { get; set; }

    public virtual Court Court { get; set; } = null!;

    public virtual ICollection<Courtbooking> Courtbookings { get; set; } = new List<Courtbooking>();
}
