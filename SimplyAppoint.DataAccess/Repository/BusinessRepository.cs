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
    public class BusinessRepository : Repository<Business>, IBusinessRepository
    {
        private readonly ApplicationDbContext _db;
        public BusinessRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Business obj)
        {
            var businessFromDb = _db.Businesses.FirstOrDefault(b => b.Id == obj.Id);
            if (businessFromDb == null) return;

            businessFromDb.Name = obj.Name;
            businessFromDb.Slug = obj.Slug;
            businessFromDb.TimeZoneId = obj.TimeZoneId;
            businessFromDb.Phone = obj.Phone;
            businessFromDb.Address = obj.Address;
        }
    }
}
