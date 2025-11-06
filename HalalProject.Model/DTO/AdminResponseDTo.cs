using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalalProject.Model.Entites;

namespace HalalProject.Model.DTO
{

    public class AdminResponseDto
    {
        public Guid RequestId { get; set; }
        public RequestStatus Status { get; set; }
        public string AdminComment { get; set; }
    }
}
