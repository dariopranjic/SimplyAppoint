using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository
{
    public class BusinessRepository : Repository<Business>, IBusinessRepository
    {
        private readonly ApplicationDbContext _db;
        public BusinessRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }
        public void Save()
        {
            _db.SaveChanges();
        }

        public void Update(Business obj)
        {
            _db.Businesses.Update(obj);
        }
    }
}
