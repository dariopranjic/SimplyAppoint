using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IBookingPolicyRepository : IRepository<BookingPolicy>
    {
        void Update(BookingPolicy obj);
    }
}
