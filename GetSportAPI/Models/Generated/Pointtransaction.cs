using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Pointtransaction
{
    public int TransactionId { get; set; }

    public int UserId { get; set; }

    public int? BookingId { get; set; }

    public int Pointchanged { get; set; }

    public string Transactiontype { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime Createat { get; set; }

    public virtual Courtbooking? Booking { get; set; }

    public virtual Account User { get; set; } = null!;
}
