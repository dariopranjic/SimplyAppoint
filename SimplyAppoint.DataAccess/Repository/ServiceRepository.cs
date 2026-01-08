using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository
{
    public class ServiceRepository : Repository<Service>, IServiceRepository
    {
        private readonly ApplicationDbContext _db;
        public ServiceRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Service obj)
        {
            var serviceFromDb = _db.Services.FirstOrDefault(s => s.Id == obj.Id);
            if (serviceFromDb == null) return;

            serviceFromDb.Name = obj.Name;
            serviceFromDb.DurationMinutes = obj.DurationMinutes;
            serviceFromDb.Price = obj.Price;
            serviceFromDb.BufferBefore = obj.BufferBefore;
            serviceFromDb.BufferAfter = obj.BufferAfter;
            serviceFromDb.IsActive = obj.IsActive;
        }
    }
}
