using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IUnitOfWork
    {
        IBusinessRepository Business { get; }

        void Save();
        void Dispose();
    }
}
