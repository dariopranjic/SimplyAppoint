using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Linq;

namespace SimplyAppoint.DataAccess.Repository
{
    public class BookingPolicyRepository : Repository<BookingPolicy>, IBookingPolicyRepository
    {
        private readonly ApplicationDbContext _db;
        public BookingPolicyRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(BookingPolicy obj)
        {
            var policyFromDb = _db.BookingPolicies.FirstOrDefault(p => p.BusinessId == obj.BusinessId);

            if (policyFromDb == null) return;

            policyFromDb.SlotIntervalMinutes = obj.SlotIntervalMinutes;
            policyFromDb.AdvanceNoticeMinutes = obj.AdvanceNoticeMinutes;
            policyFromDb.CancellationWindowMinutes = obj.CancellationWindowMinutes;
            policyFromDb.MaxAdvanceDays = obj.MaxAdvanceDays;
        }
    }
}
