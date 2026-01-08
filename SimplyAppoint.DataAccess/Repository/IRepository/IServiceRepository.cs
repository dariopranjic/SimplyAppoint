using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IServiceRepository : IRepository<Service>
    {
        void Update(Service obj);
    }
}
