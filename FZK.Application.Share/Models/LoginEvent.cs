using FZK.Database.Base.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Models
{
    /// <summary>
    /// 用户登录成功后，跳转页面参数实体
    /// </summary>
    public class LoginEvent : PubSubEvent<UserEntity>
    {
    }
}
