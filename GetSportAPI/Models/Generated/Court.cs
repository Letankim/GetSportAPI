using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Court
{
    public int CourtId { get; set; }

    public int OwnerId { get; set; }

    public string? Location { get; set; }

    public string? Imageurl { get; set; }

    public decimal Priceperhour { get; set; }

    public string? Status { get; set; }

    public bool Isactive { get; set; }

    public int Priority { get; set; }

    public DateTime? Startdate { get; set; }

    public DateTime? Enddate { get; set; }

    public virtual ICollection<Courtbooking> Courtbookings { get; set; } = new List<Courtbooking>();

    public virtual ICollection<Courtslot> Courtslots { get; set; } = new List<Courtslot>();

    public virtual ICollection<Courtstatushistory> Courtstatushistories { get; set; } = new List<Courtstatushistory>();

    public virtual Account Owner { get; set; } = null!;
}
