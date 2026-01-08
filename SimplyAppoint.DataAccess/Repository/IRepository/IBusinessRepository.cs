using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IBusinessRepository : IRepository<Business>
    {
        void Update(Business obj);
        void Save();
    }
}
