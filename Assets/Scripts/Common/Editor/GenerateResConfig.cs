using UnityEngine;
using System.IO;
using UnityEditor;

/// 生成资源配置文件的类[配置文件:初始化文件]
public class GenerateResConfig : Editor
{
    //生成菜单
    [MenuItem("Tools/Resources/Generate Config File")]
    public static void Generate()
    {
        //1 得到Resources的完整路径
        string path = Path.Combine(Application.dataPath, "Resources").Replace(@"\", "/");
        //2 从Resources路径及所有子路径中找出所有预制件 文件
        string[] resFiles = Directory.GetFiles(path, "*.prefab", SearchOption.AllDirectories);
        //3 生成键值对信息，如：BaseMeleeAttackSkill=Skill/BaseMeleeAttackSkill
        for (int i = 0; i < resFiles.Length; i++)
        {
            string key = Path.GetFileNameWithoutExtension(resFiles[i]); 
            string value = resFiles[i].Replace(@"\", "/").Replace(path + "/", "").Replace(".prefab", "");
                                                                 //把前边去掉             把扩展名去掉
            resFiles[i] = key + "=" + value;           
        }
        //4 生成资源配置文件，把键值对信息写入到资源配置文件中
        File.WriteAllLines(Path.Combine(path, "ResMap.txt"), resFiles);
        AssetDatabase.Refresh();
    }
}