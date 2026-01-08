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
    public class WorkingHoursRepository : Repository<WorkingHours>, IWorkingHoursRepository
    {
        private readonly ApplicationDbContext _db;
        public WorkingHoursRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(WorkingHours obj)
        {
            var whFromDb = _db.WorkingHours.FirstOrDefault(w => w.Id == obj.Id);
            if (whFromDb == null) return;

            whFromDb.IsClosed = obj.IsClosed;
            whFromDb.OpenTime = obj.OpenTime;
            whFromDb.CloseTime = obj.CloseTime;
        }
    }
}
