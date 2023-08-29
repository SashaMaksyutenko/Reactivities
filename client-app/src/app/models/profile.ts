import { User } from "./user";

export interface IProfile{
    username:string;
    displayname:string;
    image?:string;
    bio?:string;
}
export class Profile implements IProfile{
    constructor(user:User){
        this.username=user.username;
        this.displayname=user.displayName;
        this.image=user.image;
    }
    username:string;
    displayname:string;
    image?:string;
    bio?:string;
}