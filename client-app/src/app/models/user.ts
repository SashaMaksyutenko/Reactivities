export interface User{
    username:string;
    displayName:string;
    token:string;
    image?:string;
}
export interface UserformValues{
    email:string;
    password:string;
    displayName?:string;
    username?:string
}