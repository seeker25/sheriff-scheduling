export interface locationInfoType {
    name: string;
    id: number;
    regionId: number| null;
    agencyId?: string;
    concurrencyToken?: number;
    justinCode?: string;
    timezone: string;
}

export interface leaveInfoType {
    code: string;
    id: number;
    description?: string;
}

export interface trainingInfoType {
    code: string;
    id: number;
    description?: string;
}

export interface userInfoType {
    "roles": string[],
    "homeLocationId": number
}

export interface commonInfoType {
    "sheriffRankList": sheriffRankInfoType[]    
}

export interface sheriffRankInfoType {
    id: number,
    name: string
}
