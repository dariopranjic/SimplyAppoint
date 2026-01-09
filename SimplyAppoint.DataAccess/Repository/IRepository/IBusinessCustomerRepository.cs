using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IBusinessCustomerRepository : IRepository<BusinessCustomer>
    {
        void Update(BusinessCustomer obj);
    }
}
