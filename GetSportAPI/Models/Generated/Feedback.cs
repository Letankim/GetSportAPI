using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public int BookingId { get; set; }

    public int UserId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime Createat { get; set; }

    public virtual Courtbooking Booking { get; set; } = null!;

    public virtual Account User { get; set; } = null!;
}
