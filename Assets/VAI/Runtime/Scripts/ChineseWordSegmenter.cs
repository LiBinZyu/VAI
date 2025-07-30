using UnityEngine;
using System.Collections.Generic;
using JiebaNet.Segmenter;
using System.Text.RegularExpressions;

public class ChineseWordSegmenter : MonoBehaviour
{
    public void TestJiebaNet()
    {
        JiebaNet.Segmenter.ConfigManager.ConfigFileBaseDir = Application.dataPath + @"/VAI/Plugins/Resources";
        var helper = new JiebaSegmenterHelper();

        string sentence1 = "这个概率是零点五。";
        string sentence2 = "价格是0.5元，不是-10.5元。";
        string sentence3 = "三点一四一五九二六";

        var result1 = helper.Cut(sentence1);
        var result2 = helper.Cut(sentence2);
        var result3 = helper.Cut(sentence3);

        Debug.Log($"原句: {sentence1}");
        Debug.Log($"分词结果: {string.Join(" / ", result1)}");
        // 预期输出: 这个 / 概率 / 是 / 零点五 / 。

        Debug.Log($"原句: {sentence2}");
        Debug.Log($"分词结果: {string.Join(" / ", result2)}");
        // 预期输出: 价格 / 是 / 0.5 / 元 / ， / 不是 / -10.5 / 元 / 。

        Debug.Log($"原句: {sentence3}");
        Debug.Log($"分词结果: {string.Join(" / ", result3)}");
        // 预期输出: 三点一四一五九二六
    }
}

public class JiebaSegmenterHelper
{
    private readonly JiebaSegmenter segmenter;

    // 正则表达式，用于匹配各种数字形式
    // 1. [+\-]?\d+(\.\d+)? : 匹配标准数字，如 0.5, -10, +25.99
    // 2. [一二三四五六七八九十百千万亿零点]+ : 匹配中文数字，如 一百, 三点一四, 零点五
    private static readonly Regex NumberRegex = new Regex(@"([+\-]?\d+(\.\d+)?)|([一二三四五六七八九十百千万亿零点]+)", RegexOptions.Compiled);

    public JiebaSegmenterHelper()
    {
        segmenter = new JiebaSegmenter();
    }

    public List<string> Cut(string text)
    {
        // 1. 预处理：找出所有数字并存储
        var placeholders = new Dictionary<string, string>();
        int index = 0;

        // MatchEvaluator 可以让我们对每一个匹配项进行自定义替换
        string processedText = NumberRegex.Replace(text, match =>
        {
            string placeholder = $"__NUM_{index}__";
            segmenter.AddWord(placeholder);
            placeholders[placeholder] = match.Value; // 存储原始数字
            index++;
            return placeholder;
        });

        // 2. 对处理过的文本进行分词
        var segments = segmenter.Cut(processedText);

        // 3. 后处理：将占位符替换回原始数字
        var finalResult = new List<string>();
        foreach (var segment in segments)
        {
            if (placeholders.ContainsKey(segment))
            {
                finalResult.Add(placeholders[segment]);
            }
            else
            {
                finalResult.Add(segment);
            }
        }

        return finalResult;
    }
}
