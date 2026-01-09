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
    public class BusinessCustomerRepository : Repository<BusinessCustomer>, IBusinessCustomerRepository
    {
        private readonly ApplicationDbContext _db;
        public BusinessCustomerRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(BusinessCustomer obj)
        {
            var customerFromDb = _db.BusinessCustomers.FirstOrDefault(c => c.Id == obj.Id);
            if (customerFromDb == null) return;

            customerFromDb.FullName = obj.FullName;
            customerFromDb.Email = obj.Email;
            customerFromDb.Phone = obj.Phone;

            customerFromDb.IsActive = obj.IsActive;
            customerFromDb.UpdatedUtc = DateTimeOffset.UtcNow;

        }
    }
}
