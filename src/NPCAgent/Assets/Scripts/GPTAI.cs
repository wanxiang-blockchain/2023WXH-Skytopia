using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GPTAI
{
    public class GPTAI : MonoBehaviour
    {
        private const string RoleSymbol = "{role}";
        private const string MoodSymbol = "{mood}";
        // private const string RoleSetting = "请你扮演{role}，按照{role}的说话习惯回答";
        private const string RoleSetting = "请你扮演{role}，按照{role}的说话习惯回答，在回答开头用\"||数字||\"的形式从这些选项中\"{mood}\"选择你的心情";
        //API key
        [SerializeField] private string m_OpenAI_Key = "填写你的Key";

        // 定义Chat API的URL
        private string m_ApiUrl = "https://api.openai.com/v1/chat/completions";

        //配置参数
        [SerializeField] private GetOpenAI.PostData m_PostDataSetting;

        //聊天UI层
        [SerializeField] private GameObject m_ChatPanel;

        //输入的信息
        [SerializeField] private InputField m_InputWord;

        //返回的信息
        [SerializeField] private Text m_TextBack;

        //播放设置
        [SerializeField] private Toggle m_PlayToggle;

        //微软Azure语音
        [SerializeField] private AzureSpeech m_AzurePlayer;

        //gpt-3.5-turbo
        [SerializeField] public GptTurboScript m_GptTurboScript;
        [SerializeField] private string m_lan = "使用中文回答";
        [SerializeField] private string m_gptModel = "gpt-3.5-turbo";
        
        public GameObject Model;
        public string RoleName = "Cupid";
        private MoodAdapter _moodAdapter;

        [SerializeField] public List<SendData> m_DataList = new List<SendData>();

        private void Start()
        {

            Debug.Assert(Model,"Model is null");
            _moodAdapter = Model.GetComponent<MoodAdapter>();
            Debug.Assert(_moodAdapter,"Model not contain MoodAdapter");
            StringBuilder moods = new StringBuilder("0平淡,");

            for (int i = 0; i < _moodAdapter.Moods.Count; i++)
            {
                moods.Append($"{_moodAdapter.Moods[i]._MoodParam}{_moodAdapter.Moods[i].Des},");
            }


            var content = RoleSetting.Replace(RoleSymbol, RoleName);
            content = content.Replace(MoodSymbol, moods.ToString());
            // m_DataList.Add(new SendData("system", content));
            m_DataList.Add(new SendData("user", content));

            // StartCoroutine(GetPostData("你好", m_OpenAI_Key, CallBack));
            // m_DataList.Add(new SendData("system", Prompt));
        }

        public void Post()
        {
            if (m_InputWord.text.Equals(""))
                return;

            //记录聊天
            m_ChatHistory.Add(m_InputWord.text);

            // string _msg = m_PostDataSetting.prompt + m_lan + " " + m_InputWord.text;
            string _msg = m_InputWord.text;
            //发送数据
            //StartCoroutine (GetPostData (_msg,CallBack));
            StartCoroutine(GetPostData(_msg, m_OpenAI_Key, CallBack));

            m_InputWord.text = "";
            m_TextBack.text = "...";
            
            
        }


        //AI回复的信息
        private void CallBack(string _callback)
        {

            int mood = 0;
            try
            {
                if (_callback.StartsWith("||"))
                {
                    var substring = _callback.Substring(0, 5);
                    _callback = _callback.Replace(substring, "");
                    substring = substring.Replace("||", "");
                    mood = Int32.Parse(substring);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            
            _moodAdapter._Animator.SetBool(MoodAdapter.Param_Talking, true);
            _moodAdapter._Animator.SetInteger(MoodAdapter.Param_Mood, mood);
            
            
            _callback = _callback.Trim();
            m_TextBack.text = "";
            //开始逐个显示返回的文本
            m_WriteState=true;
            // m_TextBack.text=_callback;
            StartCoroutine(SetTextPerWord(_callback));
            
            //记录聊天
            m_ChatHistory.Add(_callback);
            
            // if(m_PlayToggle.isOn){
            //     StartCoroutine(Speek(_callback));
            // }
        }
        
        public IEnumerator GetPostData(string _postWord, string _openAI_Key, System.Action<string> _callback)
        {
            m_DataList.Add(new SendData("user", _postWord));

            using (UnityWebRequest request = new UnityWebRequest(m_ApiUrl, "POST"))
            {
                PostData _postData = new PostData
                {
                    model = m_gptModel,
                    messages = m_DataList
                };

                string _jsonText = JsonUtility.ToJson(_postData);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(_jsonText);
                request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
                request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", string.Format("Bearer {0}", _openAI_Key));

                yield return request.SendWebRequest();
                
                if (request.responseCode == 200)
                {
                    string _msg = request.downloadHandler.text;
                    MessageBack _textback = JsonUtility.FromJson<MessageBack>(_msg);
                    if (_textback != null && _textback.choices.Count > 0)
                    {

                        string _backMsg = _textback.choices[0].message.content;
                        //?????
                        m_DataList.Add(new SendData("assistant", _backMsg));
                        _callback(_backMsg);
                    }
                }
                else
                {
                    Debug.LogError($"request error : {request.responseCode} - {request.error}");
                }
            }

        }
        
        private IEnumerator Speek(string _msg){
            yield return new WaitForEndOfFrame();
            //播放合成并播放音频
            m_AzurePlayer.TurnTextToSpeech(_msg);
        }
        
        #region 文字逐个显示
        //逐字显示的时间间隔
        [SerializeField]private float m_WordWaitTime=0.2f;
        //是否显示完成
        [SerializeField]private bool m_WriteState=false;
        private IEnumerator SetTextPerWord(string _msg){
            int currentPos=0;
            while(m_WriteState){
                
                currentPos++;
                //更新显示的内容
                m_TextBack.text=_msg.Substring(0,currentPos);

                m_WriteState=currentPos<_msg.Length;
                yield return new WaitForSeconds(m_WordWaitTime);

            }
            
            _moodAdapter._Animator.SetBool(MoodAdapter.Param_Talking, false);
            _moodAdapter._Animator.SetInteger(MoodAdapter.Param_Mood, 0);
        }

        #endregion

        #region 聊天记录

        //保存聊天记录
        [SerializeField] private List<string> m_ChatHistory;

        //缓存已创建的聊天气泡
        [SerializeField] private List<GameObject> m_TempChatBox;

        //聊天记录显示层
        [SerializeField] private GameObject m_HistoryPanel;

        //聊天文本放置的层
        [SerializeField] private RectTransform m_rootTrans;

        //发送聊天气泡
        [SerializeField] private ChatPrefab m_PostChatPrefab;

        //回复的聊天气泡
        [SerializeField] private ChatPrefab m_RobotChatPrefab;

        //滚动条
        [SerializeField] private ScrollRect m_ScroTectObject;

        //获取聊天记录
        public void OpenAndGetHistory()
        {
            m_ChatPanel.SetActive(false);
            m_HistoryPanel.SetActive(true);

            ClearChatBox();
            StartCoroutine(GetHistoryChatInfo());
        }

        //返回
        public void BackChatMode()
        {
            m_ChatPanel.SetActive(true);
            m_HistoryPanel.SetActive(false);
        }

        //清空已创建的对话框
        private void ClearChatBox()
        {
            while (m_TempChatBox.Count != 0)
            {
                if (m_TempChatBox[0])
                {
                    Destroy(m_TempChatBox[0].gameObject);
                    m_TempChatBox.RemoveAt(0);
                }
            }

            m_TempChatBox.Clear();
        }

        //获取聊天记录列表
        private IEnumerator GetHistoryChatInfo()
        {
            yield return new WaitForEndOfFrame();

            for (int i = 0; i < m_ChatHistory.Count; i++)
            {
                if (i % 2 == 0)
                {
                    ChatPrefab _sendChat = Instantiate(m_PostChatPrefab, m_rootTrans.transform);
                    _sendChat.SetText(m_ChatHistory[i]);
                    m_TempChatBox.Add(_sendChat.gameObject);
                    continue;
                }

                ChatPrefab _reChat = Instantiate(m_RobotChatPrefab, m_rootTrans.transform);
                _reChat.SetText(m_ChatHistory[i]);
                m_TempChatBox.Add(_reChat.gameObject);
            }

            //重新计算容器尺寸
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_rootTrans);
            StartCoroutine(TurnToLastLine());
        }

        private IEnumerator TurnToLastLine()
        {
            yield return new WaitForEndOfFrame();
            //滚动到最近的消息
            m_ScroTectObject.verticalNormalizedPosition = 0;
        }

        #endregion

        #region ?????

        [Serializable]
        public class PostData
        {
            public string model;
            public List<SendData> messages;
        }

        [Serializable]
        public class SendData
        {
            public string role;
            public string content;

            public SendData()
            {
            }

            public SendData(string _role, string _content)
            {
                role = _role;
                content = _content;
            }
        }

        [Serializable]
        public class MessageBack
        {
            public string id;
            public string created;
            public string model;
            public List<GptTurboScript.MessageBody> choices;
        }

        [Serializable]
        public class MessageBody
        {
            public GptTurboScript.Message message;
            public string finish_reason;
            public string index;
        }

        [Serializable]
        public class Message
        {
            public string role;
            public string content;
        }

        #endregion
    }
}