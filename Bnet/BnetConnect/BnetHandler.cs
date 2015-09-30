﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bnet.BnetConnect
{
    public class BnetHandler
    {
        protected bool debugMode = false;

        public enum BnetCode
        {
            ConnectionWithServer,
            ConnectionFailed,
            ConnectionSuccess
        };

        private String[] BnetMsg =
        {
            "서버와의 연결을 시도하고 있습니다.",
            "서버와의 연결에 실패하였습니다.",
            "연결이 정상적으로 완료되었습니다."
        };

        public BnetHandler()
        {
        }

        public int getHandleMsg(BnetCode code)
        {
            if(this.debugMode == true)
            {
                Console.WriteLine(this.BnetMsg[(int) code]);
            }
            return (int) code;
        }
    }
}
