namespace AddMetaDataToVRChatPng
{
    public class PngModifyOptions
    {
        /// <summary>
        /// <para>Format string of <see cref="System.DateTime"/> for value of Creation Time of tEXt chunk.</para>
        /// <para><c>null</c> or empty string means don't add Creation Time.</para>
        /// </summary>
        public string? TextCreationTimeFormat { get; set; }
        /// <summary>
        /// Whether add tIME chunk or not.
        /// </summary>
        public bool IsAddTimeChunk { get; set; }

        /// <summary>
        /// Initialize all members.
        /// </summary>
        /// <param name="textCreationTimeFormat">Format string of <see cref="System.DateTime"/> for value of Creation Time of tEXt chunk.</param>
        /// <param name="isAddTimeChunk">Whether add tIME chunk or not.</param>
        public PngModifyOptions(
            string? textCreationTimeFormat = null,
            bool isAddTimeChunk = false)
        {
            TextCreationTimeFormat = textCreationTimeFormat;
            IsAddTimeChunk = isAddTimeChunk;
        }
    }
}
