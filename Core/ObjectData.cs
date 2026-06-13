namespace W3GAnalyzer.Core;

/// <summary>动作流里 4 字符对象 ID 的分类。</summary>
public enum ObjCat { Hero, Ability, Upgrade, Build, Item, Other }

/// <summary>
/// 魔兽 object-id（如 "Hpal"=圣骑士、"AHbz"=暴风雪、"hfoo"=步兵）的分类与名称查询。
/// 分类靠编码前缀（很可靠）：'A'=技能，'R'=升级，大写种族字母 H/O/U/E/N=英雄，
/// 小写 h/o/u/e/n=单位或建筑。名称表是社区公认的常用条目，未收录的退回原始编码，
/// 因此显示始终带上 (编码) 以便核对。
/// </summary>
public static class ObjectData
{
    public static ObjCat Categorize(string code)
    {
        if (string.IsNullOrEmpty(code)) return ObjCat.Other;
        char c = code[0];
        return c switch
        {
            'A' => ObjCat.Ability,
            'R' => ObjCat.Upgrade,
            'H' or 'O' or 'U' or 'E' or 'N' => ObjCat.Hero,
            'h' or 'o' or 'u' or 'e' or 'n' => ObjCat.Build,
            _ => ObjCat.Item,
        };
    }

    /// <summary>"名称 (编码)" 或在未收录时仅 "编码"。</summary>
    public static string Display(string code) =>
        Names.TryGetValue(code, out var n) ? $"{n} ({code})" : code;

    public static string? NameOnly(string code) =>
        Names.TryGetValue(code, out var n) ? n : null;

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        // ── 英雄 ──
        ["Hpal"] = "圣骑士", ["Hamg"] = "大法师", ["Hmkg"] = "山丘之王", ["Hblm"] = "血法师",
        ["Obla"] = "剑圣", ["Ofar"] = "先知", ["Otch"] = "牛头人酋长", ["Oshd"] = "暗影猎手",
        ["Udea"] = "死亡骑士", ["Ulic"] = "巫妖", ["Udre"] = "恐惧魔王", ["Ucrl"] = "地穴领主",
        ["Edem"] = "恶魔猎手", ["Ekee"] = "丛林守护者", ["Emoo"] = "月之女祭司", ["Ewar"] = "守望者",
        ["Npbm"] = "熊猫酒仙", ["Nbst"] = "兽王", ["Nbrn"] = "黑暗游侠", ["Nalc"] = "炼金术士",
        ["Ntin"] = "地精修补匠", ["Nfir"] = "火焰领主", ["Nngs"] = "娜迦海巫", ["Npld"] = "深渊领主",

        // ── 英雄技能 ──
        ["AHbz"] = "暴风雪", ["AHwe"] = "召唤水元素", ["AHab"] = "辉煌光环", ["AHmt"] = "群体传送",
        ["AHtb"] = "风暴之锤", ["AHtc"] = "雷霆一击", ["AHbh"] = "重击", ["AHav"] = "天神下凡",
        ["AHhb"] = "圣光术", ["AHds"] = "神圣护甲", ["AHad"] = "专注光环", ["AHre"] = "复活术",
        ["AHfs"] = "烈焰风暴", ["AHbn"] = "放逐", ["AHpx"] = "凤凰", ["AHfa"] = "法力虹吸",
        ["AOwk"] = "风行", ["AOmi"] = "镜像", ["AOcr"] = "致命一击", ["AOww"] = "剑刃风暴",
        ["AOfs"] = "千里眼", ["AOsf"] = "召唤狼魂", ["AOcl"] = "闪电链", ["AOeq"] = "地震",
        ["AOsh"] = "震荡波", ["AOws"] = "战争践踏", ["AOre"] = "转生", ["AOhw"] = "治疗波",
        ["AOhx"] = "妖术", ["AOvd"] = "巫毒",
        ["AUdc"] = "死亡缠绕", ["AUdp"] = "死亡契约", ["AUav"] = "邪恶光环", ["AUan"] = "操纵死尸",
        ["AUfn"] = "霜冻新星", ["AUfu"] = "冰甲术", ["AUsl"] = "沉睡", ["AUcs"] = "腐臭蜂群",
        ["AUin"] = "地狱火", ["AUim"] = "穿刺", ["AUts"] = "尖刺背甲", ["AUlo"] = "蝗虫群",
        ["AEmb"] = "法力燃烧", ["AEim"] = "献祭", ["AEev"] = "闪避", ["AEme"] = "恶魔变形",
        ["AEer"] = "缠绕根须", ["AEfn"] = "自然之力", ["AEah"] = "荆棘光环", ["AEtq"] = "宁静",
        ["AEsf"] = "灼热之箭", ["AEar"] = "群星坠落", ["AEbl"] = "闪烁", ["AEsh"] = "暗影突袭",
        ["AEfk"] = "刀阵旋风", ["AEsv"] = "复仇之魂",

        // ── 人族单位/建筑 ──
        ["hpea"] = "农民", ["hfoo"] = "步兵", ["hrif"] = "火枪手", ["hkni"] = "骑士",
        ["hmpr"] = "牧师", ["hsor"] = "女巫", ["hspt"] = "破法者", ["hgry"] = "狮鹫骑士",
        ["hmtm"] = "迫击炮小队", ["hgyr"] = "飞行器", ["hdhw"] = "龙鹰骑士",
        ["htow"] = "城镇大厅", ["hkee"] = "要塞", ["hcas"] = "城堡", ["hbar"] = "兵营",
        ["hlum"] = "伐木场", ["hbla"] = "铁匠铺", ["halt"] = "国王祭坛", ["hars"] = "秘法圣堂",
        ["hvlt"] = "秘法宝库", ["hhou"] = "农场", ["hwtw"] = "侦察塔", ["hgtw"] = "防御塔",
        ["hctw"] = "加农炮塔", ["hatw"] = "奥术塔", ["harm"] = "车间",

        // ── 兽族单位/建筑 ──
        ["opeo"] = "苦工", ["ogru"] = "兽人步兵", ["ohun"] = "猎头者", ["orai"] = "掠夺者",
        ["otau"] = "牛头人", ["oshm"] = "萨满祭司", ["odoc"] = "巫医", ["okod"] = "科多兽",
        ["owyv"] = "驭风者", ["ospw"] = "灵魂行者", ["otbk"] = "蝙蝠骑士",
        ["ogre"] = "大厅", ["ostr"] = "部落要塞", ["ofrt"] = "兽族堡垒", ["obar"] = "兵营",
        ["oalt"] = "风暴祭坛", ["obea"] = "兽栏", ["osld"] = "灵魂小屋", ["otto"] = "牛头人图腾",
        ["ovln"] = "巫毒商店", ["owtw"] = "战争磨坊",

        // ── 不死单位/建筑 ──
        ["uaco"] = "侍僧", ["ugho"] = "食尸鬼", ["ucry"] = "蜘蛛", ["uabo"] = "憎恶",
        ["umtw"] = "绞肉车", ["uban"] = "女妖", ["unec"] = "通灵师", ["ugar"] = "石像鬼",
        ["ufro"] = "冰霜巨龙", ["uobs"] = "黑曜石雕像", ["ushd"] = "阴影",
        ["unpl"] = "亡者大厅", ["unp1"] = "鬼屋", ["unp2"] = "黑色城堡", ["usep"] = "地穴",
        ["uaod"] = "黑暗祭坛", ["utod"] = "诅咒神庙", ["uzig"] = "通灵塔", ["usap"] = "献祭深渊",
        ["utom"] = "遗物之墓", ["ugrv"] = "墓地", ["ubon"] = "屠宰场",

        // ── 暗夜单位/建筑 ──
        ["ewsp"] = "小精灵", ["earc"] = "弓箭手", ["esen"] = "女猎手", ["edry"] = "树妖",
        ["edoc"] = "利爪德鲁伊", ["edot"] = "尖牙德鲁伊", ["ebal"] = "投石车", ["emtg"] = "山岭巨人",
        ["echm"] = "奇美拉", ["ehip"] = "角鹰兽", ["efdr"] = "小精灵龙",
        ["etol"] = "生命之树", ["etoa"] = "时代之树", ["etoe"] = "永恒之树", ["eaow"] = "战争古树",
        ["eaoe"] = "知识古树", ["eaom"] = "远古祭坛", ["eden"] = "猎手大厅", ["emow"] = "月亮井",
        ["etrp"] = "远古守护者", ["edos"] = "奇美拉栖木",
    };
}
