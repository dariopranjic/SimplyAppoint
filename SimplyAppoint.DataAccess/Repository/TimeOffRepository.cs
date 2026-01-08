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
    public class TimeOffRepository : Repository<TimeOff>, ITimeOffRepository
    {
        private readonly ApplicationDbContext _db;
        public TimeOffRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(TimeOff obj)
        {
            var timeOffFromDb = _db.TimeOffs.FirstOrDefault(t => t.Id == obj.Id);
            if (timeOffFromDb == null) return;

            timeOffFromDb.StartUtc = obj.StartUtc;
            timeOffFromDb.EndUtc = obj.EndUtc;
            timeOffFromDb.Reason = obj.Reason;
        }
    }
}
