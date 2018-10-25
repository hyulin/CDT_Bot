﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDT_Noti_Bot
{
    class CVoteDirector
    {
        List<CVoteItem> lstItem_ = new List<CVoteItem>();
        List<string> ranking_ = new List<string>();
        string voteContents_ = "";
        bool isAnonymous_ = false;

        // 투표 항목
        public void AddItem(CVoteItem item)
        {
            lstItem_.Add(item);
        }        
        public CVoteItem GetItem(int index)
        {
            return lstItem_[index];
        }
        public int GetItemCount()
        {
            return lstItem_.Count;
        }

        // 투표 내용
        public void setVoteContents(string voteContents)
        {
            voteContents_ = voteContents;
        }
        public string getVoteContents()
        {
            return voteContents_;
        }

        // 익명투표여부
        public void setAnonymous(bool isAnonymous)
        {
            isAnonymous_ = isAnonymous;
        }
        public bool getAnonymous()
        {
            return isAnonymous_;
        }

        // 투표자
        public void AddVoter(int index, string voter)
        {
            lstItem_[index].AddVoter(voter);
        }
        public List<string> getVoter(int index)
        {
            return lstItem_[index].getVoter();
        }

        // 순위
        public void AddRanking(string item)
        {
            ranking_.Add(item);
        }
        public List<string> getRanking()
        {
            return ranking_;
        }
    }
}
