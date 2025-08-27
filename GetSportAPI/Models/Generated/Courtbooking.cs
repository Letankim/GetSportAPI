using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Courtbooking
{
    public int BookingId { get; set; }

    public int UserId { get; set; }

    public int CourtId { get; set; }

    public int SlotId { get; set; }

    public DateTime Bookingdate { get; set; }

    public string? Status { get; set; }

    public decimal Amount { get; set; }

    public DateTime Createat { get; set; }

    public virtual Court Court { get; set; } = null!;

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Playmatepost> Playmateposts { get; set; } = new List<Playmatepost>();

    public virtual ICollection<Pointtransaction> Pointtransactions { get; set; } = new List<Pointtransaction>();

    public virtual Courtslot Slot { get; set; } = null!;

    public virtual Account User { get; set; } = null!;
}
