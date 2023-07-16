import { DependencyContainer } from "tsyringe";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";

const scopeTemplates = require("../db/ScopeTemplates.json");
const config = require("../config/config.json");


class Mod implements IPostDBLoadMod {

    public postDBLoad(container: DependencyContainer): void {

        const logger = container.resolve<ILogger>("WinstonLogger");
        const tables = container.resolve<DatabaseServer>("DatabaseServer").getTables();
        const itemDB = tables.templates.items;

        for (let i in itemDB) {
            let serverItem = itemDB[i];
            if (serverItem._id === "5c07dd120db834001c39092d" || serverItem._id === "5c0a2cec0db834001b7ce47d" || serverItem._parent === "55818add4bdc2d5b648b456f" || serverItem._parent === "55818ae44bdc2dde698b456c" || serverItem._parent === "55818ae44bdc2dde698b456c" || serverItem._parent === "55818aeb4bdc2ddc698b456a") {
                for (let j in scopeTemplates) {
                    let fileItem = scopeTemplates[j];
                    if (fileItem.ItemID === serverItem._id) {

                        if (serverItem._id === "5d0a3a58d7ad1a669c15ca14" || serverItem._id === "5d0a3e8cd7ad1a6f6a3d35bd") {
                            serverItem._props.Zooms = [[fileItem.MinZoom, fileItem.MinZoom, fileItem.MaxZoom, fileItem.MaxZoom]];
                        }
                        else if (serverItem._id === "57aca93d2459771f2c7e26db" || serverItem._id === "57ac965c24597706be5f975c" || serverItem._id === "5c0517910db83400232ffee5") {
                            serverItem._props.Zooms = [[fileItem.MaxZoom, fileItem.MinZoom]];
                        }
                        else if (fileItem.IsFixed == true && fileItem.CanToggle == false) {
                            serverItem._props.Zooms = [[fileItem.MinZoom]];
                        }
                        else if (serverItem._id === "5dfe6104585a0c3e995c7b82") {
                            serverItem._props.Zooms = [[fileItem.MinZoom, fileItem.MaxZoom], [1]];
                        }
                        else {
                            serverItem._props.Zooms = [[fileItem.MinZoom, fileItem.MaxZoom]];
                        }

                        if (config.disable_sens_changes == false) {
                            for (let k in serverItem._props.AimSensitivity[0]) {
                                serverItem._props.AimSensitivity[0][k] = 0.7;
                            }
                            if(serverItem._id === "5dfe6104585a0c3e995c7b82"){
                                serverItem._props.AimSensitivity[1][0] = 0.7;
                            }
                        }


                        serverItem._props.HasShoulderContact = fileItem.IsFixed;
                        serverItem._props.ToolModdable = fileItem.CanToggle;
                    }
                }
            }
        }
    }
}

module.exports = { mod: new Mod() }
