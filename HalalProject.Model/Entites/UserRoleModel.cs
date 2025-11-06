using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalalProject.Model.Entites
{
    public class UserRoleModel
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public int RoleID { get; set; }
        public virtual RoleModel Role { get; set; } = new RoleModel();
        public virtual UserModel User { get; set; } = null;
    }
}
